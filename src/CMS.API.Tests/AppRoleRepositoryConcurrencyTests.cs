using System.Security.Claims;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Repositories;
using Dapper;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests;

/// <summary>
/// Integration test against the local CMS database proving CreateAsync's PK-violation catch
/// closes the race between the controller's ExistsAsync pre-check (a separate, earlier read) and
/// the write: two concurrent CreateAsync calls for the same RoleId must result in exactly one
/// success and one RoleConflictException, never a raw unhandled SqlException.
/// </summary>
public class AppRoleRepositoryConcurrencyTests
{
    private const string ConnString =
        "Server=.\\SQLEXPRESS;Database=CMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private readonly SqlConnectionFactory _factory = new(ConnString);

    private AppRoleRepository CreateRepo()
    {
        var identity = new ClaimsIdentity(new[] { new Claim("userName", "concurrency-tester") }, authenticationType: "Test");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return new AppRoleRepository(_factory, new RowAuditWriter(_factory, accessor));
    }

    private async Task CleanupAsync(string roleId)
    {
        using var conn = await _factory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM AppUserRole WHERE RoleId = @RoleId", new { RoleId = roleId });
        await conn.ExecuteAsync(
            "DELETE FROM RowAudit WHERE TableName = 'AppRole' AND PrimaryKeyValues = @RoleId", new { RoleId = roleId });
        await conn.ExecuteAsync("DELETE FROM AppRole WHERE RoleId = @RoleId", new { RoleId = roleId });
    }

    [Fact]
    public async Task ConcurrentCreate_SameRoleId_ExactlyOneSucceeds()
    {
        var roleId = $"concurrency-test-{Guid.NewGuid():N}"[..30];

        try
        {
            var repoA = CreateRepo();
            var repoB = CreateRepo();
            var request = new AppRoleRequest { RoleId = roleId, RoleName = "Concurrency Test", PermissionLevel = 999 };

            var taskA = repoA.CreateAsync(request, CancellationToken.None);
            var taskB = repoB.CreateAsync(request, CancellationToken.None);

            var results = await Task.WhenAll(
                taskA.ContinueWith(t => (ok: !t.IsFaulted, ex: t.IsFaulted ? t.Exception!.InnerException : null)),
                taskB.ContinueWith(t => (ok: !t.IsFaulted, ex: t.IsFaulted ? t.Exception!.InnerException : null)));

            Assert.Single(results, r => r.ok);
            var failure = Assert.Single(results, r => !r.ok);
            Assert.IsType<RoleConflictException>(failure.ex);

            using var conn = await _factory.CreateOpenConnectionAsync();
            var rowCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM AppRole WHERE RoleId = @RoleId", new { RoleId = roleId });
            Assert.Equal(1, rowCount);
        }
        finally
        {
            await CleanupAsync(roleId);
        }
    }
}
