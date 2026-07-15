using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/course-groups")]
public class CourseGroupsController : ControllerBase
{
    private readonly ICourseGroupRepository _repository;

    public CourseGroupsController(ICourseGroupRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All course groups.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CourseGroup>>> GetAll(CancellationToken ct)
    {
        var groups = await _repository.GetAllAsync(ct);
        return Ok(groups);
    }

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    public async Task<ActionResult<IEnumerable<CourseGroup>>> Query([FromBody] CourseGroupQuery query, CancellationToken ct)
    {
        var groups = await _repository.QueryAsync(query ?? new CourseGroupQuery(), ct);
        return Ok(groups);
    }

    /// <summary>Single course group by pkid (smallint PK).</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CourseGroup>> GetById(short id, CancellationToken ct)
    {
        var group = await _repository.GetByIdAsync(id, ct);
        return group is null ? NotFound() : Ok(group);
    }

    /// <summary>Create a course group. The pkid is DB-assigned (IDENTITY).</summary>
    [HttpPost]
    public async Task<ActionResult<CourseGroup>> Create([FromBody] CourseGroupRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            ModelState.AddModelError(nameof(request.Description), "Description is required.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var created = await _repository.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Pkid }, created);
    }

    /// <summary>Update a course group (pkid taken from the body — it is the primary key).</summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] CourseGroupRequest request, CancellationToken ct)
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

    /// <summary>
    /// Delete a course group by pkid. NOTE: FK_Course_CourseGroup is ON DELETE CASCADE — every Course
    /// filed under this group is deleted with it.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(short id, CancellationToken ct)
    {
        var deleted = await _repository.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
