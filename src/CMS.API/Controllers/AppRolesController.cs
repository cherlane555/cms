using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/app-roles")]
public class AppRolesController : ControllerBase
{
    private readonly IAppRoleRepository _repository;

    public AppRolesController(IAppRoleRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All roles.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppRole>>> GetAll(CancellationToken ct)
    {
        var roles = await _repository.GetAllAsync(ct);
        return Ok(roles);
    }

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    public async Task<ActionResult<IEnumerable<AppRole>>> Query([FromBody] AppRoleQuery query, CancellationToken ct)
    {
        var roles = await _repository.QueryAsync(query ?? new AppRoleQuery(), ct);
        return Ok(roles);
    }

    /// <summary>Single role by RoleId (string PK), including assigned user ids.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AppRole>> GetById(string id, CancellationToken ct)
    {
        var role = await _repository.GetByIdAsync(id, ct);
        return role is null ? NotFound() : Ok(role);
    }

    /// <summary>
    /// Create a role (Admin only). UserIds here writes directly to AppUserRole, and RoleId is
    /// exactly what AuthRepository reads to build a JWT's role claims — an unrestricted caller
    /// could self-assign to "Admin" and re-login with full admin access, so this must never be
    /// reachable by a non-Admin account.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<AppRole>> Create([FromBody] AppRoleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RoleId))
        {
            ModelState.AddModelError(nameof(request.RoleId), "RoleId is required.");
            return ValidationProblem(ModelState);
        }

        if (await _repository.ExistsAsync(request.RoleId, ct))
        {
            return Conflict($"Role '{request.RoleId}' already exists.");
        }

        try
        {
            var created = await _repository.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.RoleId }, created);
        }
        catch (RoleConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Update a role (RoleId taken from the body — it is the primary key). Admin only — see
    /// <see cref="Create"/> for why: UserIds here rewrites AppUserRole directly.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] AppRoleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RoleId))
        {
            ModelState.AddModelError(nameof(request.RoleId), "RoleId is required.");
            return ValidationProblem(ModelState);
        }

        var updated = await _repository.UpdateAsync(request, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Delete a role by RoleId (Admin only).</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var deleted = await _repository.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
