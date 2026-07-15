using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseRepository
{
    Task<IEnumerable<Course>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Course>> QueryAsync(CourseQuery query, CancellationToken ct = default);
    Task<Course?> GetByIdAsync(int pkid, CancellationToken ct = default);
    Task<Course> CreateAsync(CourseRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(CourseRequest request, CancellationToken ct = default);
    /// <summary>Rows in CourseFAQ / CourseRelatedLink / HotCourse that block a delete (no cascade).</summary>
    Task<int> CountBlockingDependentsAsync(int pkid, CancellationToken ct = default);
    Task<bool> DeleteAsync(int pkid, CancellationToken ct = default);
}
