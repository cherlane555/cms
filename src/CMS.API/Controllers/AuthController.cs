using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthRepository _repository;
    private readonly ITokenService _tokenService;

    public AuthController(IAuthRepository repository, ITokenService tokenService)
    {
        _repository = repository;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Validate credentials and, on success, return the user's profile with a signed JWT.
    /// Any failed check yields the same generic 401 — never reveal which part failed.
    /// </summary>
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

        // UserId exact match (the lookup) + IsActive = 1 + PasswordHash = SHA256(password).
        var suppliedHash = PasswordHasher.Sha256Hex(request.Password);
        if (user is null
            || !user.IsActive
            || !string.Equals(user.PasswordHash?.Trim(), suppliedHash, StringComparison.OrdinalIgnoreCase))
        {
            return InvalidCredentials();
        }

        var signingKey = await _repository.GetSigningKeyAsync(ct);
        var token = _tokenService.CreateToken(user.UserId, user.UserName, user.Roles, signingKey);

        return Ok(new LoginResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            AccessToken = token.AccessToken
        });
    }

    private UnauthorizedObjectResult InvalidCredentials() =>
        Unauthorized(new { message = "invalid credentials" });
}
