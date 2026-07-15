using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Http;
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

    // ---- Update profile (UserName only, JWT-scoped) ----

    // Builds a controller whose User principal carries the given "userId" claim (as the JWT does).
    private AuthController CreateControllerWithUser(string? userId)
    {
        var claims = userId is null ? Array.Empty<Claim>() : new[] { new Claim("userId", userId) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        return new AuthController(_repo.Object, _tokenService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    [Fact]
    public async Task UpdateProfile_UpdatesUserNameForJwtUser()
    {
        _repo.Setup(r => r.UpdateUserNameAsync(UserId, "New Name", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateControllerWithUser(UserId)
            .UpdateProfile(new UpdateProfileRequest { UserName = "New Name" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal(UserId, body.UserId);
        Assert.Equal("New Name", body.UserName);
        _repo.Verify(r => r.UpdateUserNameAsync(UserId, "New Name", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_TrimsUserName()
    {
        _repo.Setup(r => r.UpdateUserNameAsync(UserId, "Trimmed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateControllerWithUser(UserId)
            .UpdateProfile(new UpdateProfileRequest { UserName = "   Trimmed   " }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _repo.Verify(r => r.UpdateUserNameAsync(UserId, "Trimmed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task UpdateProfile_EmptyUserName_ReturnsBadRequest(string? userName)
    {
        var result = await CreateControllerWithUser(UserId)
            .UpdateProfile(new UpdateProfileRequest { UserName = userName }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repo.Verify(
            r => r.UpdateUserNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_NoUserIdClaim_ReturnsUnauthorized()
    {
        var result = await CreateControllerWithUser(null)
            .UpdateProfile(new UpdateProfileRequest { UserName = "New Name" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        _repo.Verify(
            r => r.UpdateUserNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---- Change password (JWT-scoped, current verified, complexity enforced) ----

    private const string NewPassword = "Str0ng!Pwd"; // 8+, upper+lower+digit+symbol

    private static ChangePasswordRequest ChangePasswordBody(
        string current = Password, string @new = NewPassword, string? confirm = null) =>
        new() { CurrentPassword = current, NewPassword = @new, ConfirmPassword = confirm ?? @new };

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ChangesNothing()
    {
        _repo.Setup(r => r.GetLoginUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(SampleUser());

        var result = await CreateControllerWithUser(UserId)
            .ChangePassword(ChangePasswordBody(current: "wrong-password"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _repo.Verify(
            r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Ab1!")]        // too short (< 8)
    [InlineData("abcdefghij")]  // 1 class (lowercase only)
    [InlineData("abcdefgh1")]   // 2 classes (lowercase + digit)
    public async Task ChangePassword_WeakNewPassword_RejectedWithComplexityMessage(string weak)
    {
        _repo.Setup(r => r.GetLoginUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(SampleUser());

        var result = await CreateControllerWithUser(UserId)
            .ChangePassword(ChangePasswordBody(@new: weak), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains(PasswordPolicy.ComplexityMessage, bad.Value!.ToString());
        _repo.Verify(
            r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePassword_NewAndConfirmMismatch_ChangesNothing()
    {
        _repo.Setup(r => r.GetLoginUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(SampleUser());

        var result = await CreateControllerWithUser(UserId)
            .ChangePassword(ChangePasswordBody(@new: NewPassword, confirm: "Different1!"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _repo.Verify(
            r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePassword_Valid_SetsHashOfNewPassword()
    {
        _repo.Setup(r => r.GetLoginUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(SampleUser());
        _repo.Setup(r => r.UpdatePasswordAsync(UserId, PasswordHasher.Sha256Hex(NewPassword), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateControllerWithUser(UserId)
            .ChangePassword(ChangePasswordBody(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _repo.Verify(
            r => r.UpdatePasswordAsync(UserId, PasswordHasher.Sha256Hex(NewPassword), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---- Reset password to default (Admin only; role enforcement tested in AuthorizationTests) ----

    private const string DefaultPassword = "CMS4fun#";
    private const string TargetUser = "someone@else.com";

    [Fact]
    public async Task ResetPassword_SetsHashOfDefaultPassword()
    {
        _repo.Setup(r => r.GetDefaultPasswordAsync(It.IsAny<CancellationToken>())).ReturnsAsync(DefaultPassword);
        _repo.Setup(r => r.UpdatePasswordAsync(TargetUser, PasswordHasher.Sha256Hex(DefaultPassword), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateControllerWithUser(UserId)
            .ResetPassword(new ResetPasswordRequest { UserId = TargetUser }, CancellationToken.None);

        // NoContent carries no body — no password/hash is returned.
        Assert.IsType<NoContentResult>(result);
        _repo.Verify(
            r => r.UpdatePasswordAsync(TargetUser, PasswordHasher.Sha256Hex(DefaultPassword), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ResetPassword_MissingUserId_ReturnsBadRequest(string? targetUserId)
    {
        var result = await CreateControllerWithUser(UserId)
            .ResetPassword(new ResetPasswordRequest { UserId = targetUserId }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _repo.Verify(
            r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResetPassword_UnknownUser_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetDefaultPasswordAsync(It.IsAny<CancellationToken>())).ReturnsAsync(DefaultPassword);
        _repo.Setup(r => r.UpdatePasswordAsync("ghost@x.com", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateControllerWithUser(UserId)
            .ResetPassword(new ResetPasswordRequest { UserId = "ghost@x.com" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
