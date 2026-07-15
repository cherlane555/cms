using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

public class AuthControllerTests
{
    // HS256 requires a >= 256-bit (32-byte) key.
    private const string SigningKey = "unit-test-signing-key-0123456789-abcdef";
    private const string Password = "CMS4fun#";
    private const string UserId = "miles@uuu.com.tw";

    private readonly Mock<IAuthRepository> _repo = new(MockBehavior.Strict);
    private readonly ITokenService _tokenService = new TokenService();

    private AuthController CreateController() => new(_repo.Object, _tokenService);

    private static AuthUser SampleUser(bool active = true) => new()
    {
        UserId = UserId,
        UserName = "Miles Sun",
        PasswordHash = PasswordHasher.Sha256Hex(Password),
        IsActive = active,
        Roles = new List<string> { "Admin", "User" }
    };

    private static LoginRequest Credentials(string password = Password) =>
        new() { UserId = UserId, Password = password };

    private async Task<LoginResponse> LoginSuccessAsync()
    {
        _repo.Setup(r => r.GetLoginUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(SampleUser());
        _repo.Setup(r => r.GetSigningKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SigningKey);

        var result = await CreateController().Login(Credentials(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<LoginResponse>(ok.Value);
    }

    // ---- Success ----

    [Fact]
    public async Task Login_ValidActiveUser_ReturnsProfileWithToken()
    {
        var body = await LoginSuccessAsync();

        Assert.Equal(UserId, body.UserId);
        Assert.Equal("Miles Sun", body.UserName);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    // ---- 401 cases (generic message, no token) ----

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        _repo.Setup(r => r.GetLoginUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(SampleUser());

        var result = await CreateController().Login(Credentials("not-the-password"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        _repo.Verify(r => r.GetSigningKeyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        _repo.Setup(r => r.GetLoginUserAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync((AuthUser?)null);

        var result = await CreateController().Login(
            new LoginRequest { UserId = "ghost", Password = Password }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        _repo.Verify(r => r.GetSigningKeyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_InactiveUser_Returns401()
    {
        _repo.Setup(r => r.GetLoginUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(SampleUser(active: false));

        var result = await CreateController().Login(Credentials(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        _repo.Verify(r => r.GetSigningKeyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Token contents ----

    [Fact]
    public async Task Login_Token_CarriesUsersRoleClaims()
    {
        var body = await LoginSuccessAsync();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);

        // Match whether the role claim serializes as "role", ClaimTypes.Role, or the long URI.
        var roleValues = jwt.Claims
            .Where(c => c.Type == "role"
                        || c.Type == ClaimTypes.Role
                        || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToList();

        Assert.Contains("Admin", roleValues);
        Assert.Contains("User", roleValues);
    }

    [Fact]
    public async Task Login_Token_CarriesUserIdAndUserNameClaims()
    {
        var body = await LoginSuccessAsync();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);

        Assert.Contains(jwt.Claims, c => c.Type == "userId" && c.Value == UserId);
        Assert.Contains(jwt.Claims, c => c.Type == "userName" && c.Value == "Miles Sun");
    }

    [Fact]
    public async Task Login_Token_ExpiresApproximately24HoursFromNow()
    {
        var body = await LoginSuccessAsync();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);

        var expected = DateTime.UtcNow.AddHours(24);
        Assert.True(Math.Abs((jwt.ValidTo - expected).TotalMinutes) < 5,
            $"exp {jwt.ValidTo:o} should be ~24h from now ({expected:o}).");
    }

    // ---- No PasswordHash leak ----

    [Fact]
    public async Task Login_Response_NeverExposesPasswordHash()
    {
        var body = await LoginSuccessAsync();

        Assert.Null(typeof(LoginResponse).GetProperty("PasswordHash"));

        var json = JsonSerializer.Serialize(body);
        Assert.DoesNotContain("passwordhash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PasswordHasher.Sha256Hex(Password), json, StringComparison.OrdinalIgnoreCase);
    }
}
