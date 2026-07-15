using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly ICourseRepository _repository;

    public CoursesController(ICourseRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All courses.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Course>>> GetAll(CancellationToken ct)
    {
        var courses = await _repository.GetAllAsync(ct);
        return Ok(courses);
    }

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    public async Task<ActionResult<IEnumerable<Course>>> Query([FromBody] CourseQuery query, CancellationToken ct)
    {
        var courses = await _repository.QueryAsync(query ?? new CourseQuery(), ct);
        return Ok(courses);
    }

    /// <summary>Single course by pkid, including its certification/job-category associations.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Course>> GetById(int id, CancellationToken ct)
    {
        var course = await _repository.GetByIdAsync(id, ct);
        return course is null ? NotFound() : Ok(course);
    }

    /// <summary>Create a course. The pkid is DB-assigned (IDENTITY).</summary>
    [HttpPost]
    public async Task<ActionResult<Course>> Create([FromBody] CourseRequest request, CancellationToken ct)
    {
        Validate(request);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var created = await _repository.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Pkid }, created);
    }

    /// <summary>Update a course (pkid taken from the body — it is the primary key).</summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] CourseRequest request, CancellationToken ct)
    {
        if (request.Pkid == 0)
        {
            ModelState.AddModelError(nameof(request.Pkid), "Pkid is required.");
        }
        Validate(request);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var updated = await _repository.UpdateAsync(request, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>
    /// Delete a course by pkid. Blocked (409) while CourseFAQ / CourseRelatedLink / HotCourse rows
    /// still reference it — those FKs have no cascade.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var dependents = await _repository.CountBlockingDependentsAsync(id, ct);
        if (dependents > 0)
        {
            return Conflict($"Course {id} is referenced by {dependents} dependent row(s) (FAQ/related-link/hot-course).");
        }

        var deleted = await _repository.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    private void Validate(CourseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            ModelState.AddModelError(nameof(request.Title), "Title is required.");
        }
        if (string.IsNullOrWhiteSpace(request.CourseId))
        {
            ModelState.AddModelError(nameof(request.CourseId), "CourseId is required.");
        }
        if (string.IsNullOrWhiteSpace(request.ProdCourseId))
        {
            ModelState.AddModelError(nameof(request.ProdCourseId), "ProdCourseId is required.");
        }
        if (string.IsNullOrWhiteSpace(request.FriendlyUrl))
        {
            ModelState.AddModelError(nameof(request.FriendlyUrl), "FriendlyUrl is required.");
        }
        if (request.PartnerPkid <= 0)
        {
            ModelState.AddModelError(nameof(request.PartnerPkid), "PartnerPkid is required.");
        }
        if (request.PublishStatusPkid <= 0)
        {
            ModelState.AddModelError(nameof(request.PublishStatusPkid), "PublishStatusPkid is required.");
        }
        if (request.ScheduleOn == default)
        {
            ModelState.AddModelError(nameof(request.ScheduleOn), "ScheduleOn is required.");
        }
        if (request.ScheduleOff == default)
        {
            ModelState.AddModelError(nameof(request.ScheduleOff), "ScheduleOff is required.");
        }
    }
}
