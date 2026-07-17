using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Thrown by <see cref="IAppRoleRepository.CreateAsync"/> when RoleId already exists.
/// Raised from a caught PK-violation on the insert itself, closing the race window between the
/// controller's ExistsAsync pre-check (a separate, earlier read) and the write.</summary>
public class RoleConflictException : Exception
{
    public RoleConflictException(string message) : base(message)
    {
    }
}

public interface IAppRoleRepository
{
    Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken ct = default);
    Task<AppRole?> GetByIdAsync(string roleId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string roleId, CancellationToken ct = default);
    Task<AppRole> CreateAsync(AppRoleRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(string roleId, CancellationToken ct = default);
}
