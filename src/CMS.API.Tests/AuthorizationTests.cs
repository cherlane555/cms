using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace CMS.API.Tests;

/// <summary>
/// Integration tests over the real middleware pipeline (WebApplicationFactory). DB-touching
/// services are replaced with stubs/mocks so no live database is needed:
/// the signing key is a fixed test key, and the repositories are mocked.
/// </summary>
public class AuthorizationTests : IClassFixture<AuthorizationTests.ApiFactory>
{
    // HS256 needs a >= 256-bit (32-byte) key.
    private const string TestKey = "authz-integration-test-signing-key-0123456789";

    private readonly ApiFactory _factory;

    public AuthorizationTests(ApiFactory factory) => _factory = factory;

    private sealed class StubKeyProvider : IJwtSigningKeyProvider
    {
        public string GetSigningKey() => TestKey;
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public Mock<IAppRoleRepository> AppRoleRepo { get; } = new();
        public Mock<IAuthRepository> AuthRepo { get; } = new();
        public Mock<IPublishStatusRepository> PublishStatusRepo { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IJwtSigningKeyProvider>();
                services.AddSingleton<IJwtSigningKeyProvider>(new StubKeyProvider());

                services.RemoveAll<IAppRoleRepository>();
                services.AddScoped(_ => AppRoleRepo.Object);

                services.RemoveAll<IAuthRepository>();
                services.AddScoped(_ => AuthRepo.Object);

                services.RemoveAll<IPublishStatusRepository>();
                services.AddScoped(_ => PublishStatusRepo.Object);
            });
        }
    }

    private static string ValidToken() =>
        new TokenService()
            .CreateToken("miles@uuu.com.tw", "Miles Sun", new[] { "Admin", "User" }, TestKey)
            .AccessToken;

    private static string NonAdminToken() =>
        new TokenService()
            .CreateToken("reg@uuu.com.tw", "Reg User", new[] { "User" }, TestKey)
            .AccessToken;

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-jwt");

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        _factory.AppRoleRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppRole>());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ValidToken());

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/Auth/profile", new { userName = "Whoever" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_UsesJwtUserId_AndIgnoresBodyUserId()
    {
        string? renamedUser = null;
        _factory.AuthRepo
            .Setup(r => r.UpdateUserNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((uid, _, _) => renamedUser = uid)
            .ReturnsAsync(true);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ValidToken());

        // Body smuggles a different UserId; the endpoint must ignore it and use the JWT's user.
        var response = await client.PutAsJsonAsync(
            "/api/Auth/profile",
            new { userName = "Renamed", userId = "attacker@evil.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("miles@uuu.com.tw", renamedUser);
        _factory.AuthRepo.Verify(
            r => r.UpdateUserNameAsync("miles@uuu.com.tw", "Renamed", It.IsAny<CancellationToken>()), Times.Once);
        _factory.AuthRepo.Verify(
            r => r.UpdateUserNameAsync("attacker@evil.com", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/reset-password", new { userId = "target@x.com" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_NonAdmin_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NonAdminToken());

        var response = await client.PostAsJsonAsync("/api/Auth/reset-password", new { userId = "target@x.com" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_Admin_SetsDefaultHash_AndReturnsNoBody()
    {
        string? capturedHash = null;
        _factory.AuthRepo
            .Setup(r => r.GetDefaultPasswordAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("CMS4fun#");
        _factory.AuthRepo
            .Setup(r => r.UpdatePasswordAsync("target@x.com", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, hash, _) => capturedHash = hash)
            .ReturnsAsync(true);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ValidToken());

        var response = await client.PostAsJsonAsync("/api/Auth/reset-password", new { userId = "target@x.com" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(PasswordHasher.IsLegacyFormat(capturedHash));
        Assert.True(PasswordHasher.Verify("CMS4fun#", capturedHash));

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("CMS4fun#", body);
        Assert.DoesNotContain(PasswordHasher.Sha256Hex("CMS4fun#"), body);
    }

    // ---- AppRole write endpoints (Admin only — UserIds here writes AppUserRole, which is
    // exactly what login reads to build JWT role claims; a non-Admin caller must never reach it) ----

    [Fact]
    public async Task AppRoleCreate_NonAdmin_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NonAdminToken());

        var response = await client.PostAsJsonAsync(
            "/api/app-roles",
            new { roleId = "Editor", roleName = "Editor", permissionLevel = 50, userIds = new[] { "reg@uuu.com.tw" } });

        // ASP.NET Core's authorization middleware runs before the controller action, so a 403
        // here already proves CreateAsync was never reached — a separate Times.Never mock verify
        // would be redundant and, since AppRoleRepo is shared across this whole test class via
        // IClassFixture, vulnerable to false failures if another test's matching invocation runs
        // first (Moq records invocation history for the mock's full lifetime, not per-test).
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AppRoleCreate_Admin_Succeeds()
    {
        _factory.AppRoleRepo
            .Setup(r => r.ExistsAsync("Editor", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _factory.AppRoleRepo
            .Setup(r => r.CreateAsync(It.IsAny<AppRoleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppRole { RoleId = "Editor", RoleName = "Editor", PermissionLevel = 50 });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ValidToken());

        var response = await client.PostAsJsonAsync(
            "/api/app-roles",
            new { roleId = "Editor", roleName = "Editor", permissionLevel = 50, userIds = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---- PublishStatus write endpoints (Admin only — nav-gated as Admin but had no server-side
    // enforcement; Course.PublishStatus_pkid FKs to this table) ----

    [Fact]
    public async Task PublishStatusCreate_NonAdmin_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NonAdminToken());

        var response = await client.PostAsJsonAsync(
            "/api/publish-statuses",
            new { pkid = 9, description = "Test Status" });

        // See AppRoleCreate_NonAdmin_Returns403 for why there's no Times.Never verify here.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PublishStatusCreate_Admin_Succeeds()
    {
        _factory.PublishStatusRepo
            .Setup(r => r.ExistsAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _factory.PublishStatusRepo
            .Setup(r => r.CreateAsync(It.IsAny<PublishStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishStatus { Pkid = 9, Description = "Test Status" });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ValidToken());

        var response = await client.PostAsJsonAsync(
            "/api/publish-statuses",
            new { pkid = 9, description = "Test Status" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AuthLogin_IsAnonymous_ReturnsOkWithoutToken()
    {
        _factory.AuthRepo
            .Setup(r => r.GetLoginUserAsync("miles@uuu.com.tw", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthUser
            {
                UserId = "miles@uuu.com.tw",
                UserName = "Miles Sun",
                IsActive = true,
                PasswordHash = PasswordHasher.Sha256Hex("CMS4fun#"),
                Roles = new List<string> { "Admin" }
            });
        _factory.AuthRepo
            .Setup(r => r.GetSigningKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestKey);

        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/Auth/login",
            new { userId = "miles@uuu.com.tw", password = "CMS4fun#" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
