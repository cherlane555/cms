using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/lookups")]
public class LookupsController : ControllerBase
{
    private readonly ILookupRepository _repository;

    public LookupsController(ILookupRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Slim AppUser list for the role's user multi-select.</summary>
    [HttpGet("app-users")]
    public async Task<ActionResult<IEnumerable<AppUserLookup>>> GetAppUsers(CancellationToken ct)
    {
        var users = await _repository.GetAppUsersAsync(ct);
        return Ok(users);
    }

    /// <summary>Slim PublishStatus list for FK dropdowns (e.g. Course).</summary>
    [HttpGet("publish-statuses")]
    public async Task<ActionResult<IEnumerable<PublishStatusLookup>>> GetPublishStatuses(CancellationToken ct)
    {
        var statuses = await _repository.GetPublishStatusesAsync(ct);
        return Ok(statuses);
    }
}
