using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/publish-statuses")]
public class PublishStatusesController : ControllerBase
{
    private readonly IPublishStatusRepository _repository;

    public PublishStatusesController(IPublishStatusRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All publish statuses.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> GetAll(CancellationToken ct)
    {
        var statuses = await _repository.GetAllAsync(ct);
        return Ok(statuses);
    }

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> Query([FromBody] PublishStatusQuery query, CancellationToken ct)
    {
        var statuses = await _repository.QueryAsync(query ?? new PublishStatusQuery(), ct);
        return Ok(statuses);
    }

    /// <summary>Single publish status by pkid (tinyint PK).</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PublishStatus>> GetById(byte id, CancellationToken ct)
    {
        var status = await _repository.GetByIdAsync(id, ct);
        return status is null ? NotFound() : Ok(status);
    }

    /// <summary>Create a publish status. The pkid is user-assigned (supplied in the body).</summary>
    [HttpPost]
    public async Task<ActionResult<PublishStatus>> Create([FromBody] PublishStatusRequest request, CancellationToken ct)
    {
        if (request.Pkid == 0)
        {
            ModelState.AddModelError(nameof(request.Pkid), "Pkid is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            ModelState.AddModelError(nameof(request.Description), "Description is required.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (await _repository.ExistsAsync(request.Pkid, ct))
        {
            return Conflict($"PublishStatus '{request.Pkid}' already exists.");
        }

        var created = await _repository.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Pkid }, created);
    }

    /// <summary>Update a publish status (pkid taken from the body — it is the primary key).</summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] PublishStatusRequest request, CancellationToken ct)
    {
        if (request.Pkid == 0)
        {
            ModelState.AddModelError(nameof(request.Pkid), "Pkid is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            ModelState.AddModelError(nameof(request.Description), "Description is required.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var updated = await _repository.UpdateAsync(request, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Delete a publish status by pkid.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(byte id, CancellationToken ct)
    {
        var deleted = await _repository.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
