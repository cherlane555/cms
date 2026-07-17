using System.Security.Claims;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthRepository _repository;
    private readonly ITokenService _tokenService;
    private readonly IJwtSigningKeyProvider _keyProvider;

    public AuthController(IAuthRepository repository, ITokenService tokenService, IJwtSigningKeyProvider keyProvider)
    {
        _repository = repository;
        _tokenService = tokenService;
        _keyProvider = keyProvider;
    }

    /// <summary>
    /// Validate credentials and, on success, return the user's profile with a signed JWT.
    /// Any failed check yields the same generic 401 — never reveal which part failed.
    /// </summary>
    [AllowAnonymous] // Login is the one public endpoint; everything else is covered by the global fallback policy.
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.UserId)
            || string.IsNullOrEmpty(request.Password))
        {
            return InvalidCredentials();
        }

        var user = await _repository.GetLoginUserAsync(request.UserId, ct);

        // UserId exact match (the lookup) + IsActive = 1 + a hash of the supplied password
        // verifying against the stored PasswordHash (legacy unsalted-SHA256 or current PBKDF2).
        if (user is null
            || !user.IsActive
            || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return InvalidCredentials();
        }

        // Transparent migration: a legacy (Lab 03) row can only be rehashed once we have the
        // plaintext in hand, which is exactly now — right after verifying it. One-time per user.
        if (PasswordHasher.IsLegacyFormat(user.PasswordHash))
        {
            await _repository.UpdatePasswordAsync(user.UserId, PasswordHasher.Hash(request.Password), ct);
        }

        // Sign with the same cached key JwtBearer validation uses (IJwtSigningKeyProvider), not
        // a fresh DB read — otherwise a token minted right after a key rotation (new key) would
        // fail validation on its very next request (still checked against the stale cached key)
        // until the app restarts and the cache catches up.
        var token = _tokenService.CreateToken(user.UserId, user.UserName, user.Roles, _keyProvider.GetSigningKey());

        return Ok(new LoginResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            AccessToken = token.AccessToken
        });
    }

    /// <summary>
    /// Update the signed-in user's display name. The target UserId is taken from the JWT — any
    /// UserId in the request body is ignored — so a user can only ever rename themselves, and
    /// roles are never touched.
    /// </summary>
    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue("userId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = "invalid token" });
        }

        var userName = request?.UserName?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return BadRequest(new { message = "UserName is required." });
        }

        var updated = await _repository.UpdateUserNameAsync(userId, userName, ct);
        if (!updated)
        {
            return NotFound();
        }

        return Ok(new ProfileResponse { UserId = userId, UserName = userName });
    }

    /// <summary>
    /// Change the signed-in user's password. The target user is the JWT's user. Requires the
    /// correct current password, enforces new-password complexity, and confirms new == confirm.
    /// All hashing stays server-side.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue("userId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = "invalid token" });
        }

        request ??= new ChangePasswordRequest();

        var user = await _repository.GetLoginUserAsync(userId, ct);
        if (user is null)
        {
            return Unauthorized(new { message = "invalid token" });
        }

        // 1. Current password must match the stored hash.
        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "目前密碼不正確 Current password is incorrect." });
        }

        // 2. New password must meet complexity rules.
        if (!PasswordPolicy.IsValid(request.NewPassword))
        {
            return BadRequest(new { message = PasswordPolicy.ComplexityMessage });
        }

        // 3. New and confirmation must match.
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "新密碼與確認密碼不一致 New password and confirmation do not match." });
        }

        // 4. Persist the new hash + updated timestamp.
        var newHash = PasswordHasher.Hash(request.NewPassword!);
        await _repository.UpdatePasswordAsync(userId, newHash, ct);

        return NoContent();
    }

    /// <summary>
    /// Reset a target user's password back to the system default (Admin only). The default is
    /// read from SysConfig at runtime, hashed, and stored — no password/hash crosses the wire.
    /// The Admin role is enforced by <see cref="AuthorizeAttribute"/>; a non-Admin caller gets 403.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new { message = "UserId is required." });
        }

        var defaultPassword = await _repository.GetDefaultPasswordAsync(ct);
        var hash = PasswordHasher.Hash(defaultPassword);

        var updated = await _repository.UpdatePasswordAsync(request.UserId, hash, ct);
        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    private UnauthorizedObjectResult InvalidCredentials() =>
        Unauthorized(new { message = "invalid credentials" });
}
