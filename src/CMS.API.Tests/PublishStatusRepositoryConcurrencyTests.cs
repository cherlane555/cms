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
/// the write: two concurrent CreateAsync calls for the same pkid must result in exactly one
/// success and one PublishStatusConflictException, never a raw unhandled SqlException.
/// </summary>
public class PublishStatusRepositoryConcurrencyTests
{
    private const string ConnString =
        "Server=.\\SQLEXPRESS;Database=CMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private readonly SqlConnectionFactory _factory = new(ConnString);

    private PublishStatusRepository CreateRepo()
    {
        var identity = new ClaimsIdentity(new[] { new Claim("userName", "concurrency-tester") }, authenticationType: "Test");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return new PublishStatusRepository(_factory, new RowAuditWriter(_factory, accessor));
    }

    private async Task CleanupAsync(byte pkid)
    {
        using var conn = await _factory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM RowAudit WHERE TableName = 'PublishStatus' AND PrimaryKeyValues = @Pk", new { Pk = pkid.ToString() });
        await conn.ExecuteAsync("DELETE FROM PublishStatus WHERE pkid = @Pkid", new { Pkid = pkid });
    }

    [Fact]
    public async Task ConcurrentCreate_SamePkid_ExactlyOneSucceeds()
    {
        // A pkid far outside the small real range (1-3 per docs/auth-notes.md) so it can never
        // collide with real app data or a parallel test run.
        const byte pkid = 250;

        try
        {
            var repoA = CreateRepo();
            var repoB = CreateRepo();
            var request = new PublishStatusRequest { Pkid = pkid, Description = "Concurrency Test" };

            var taskA = repoA.CreateAsync(request, CancellationToken.None);
            var taskB = repoB.CreateAsync(request, CancellationToken.None);

            var results = await Task.WhenAll(
                taskA.ContinueWith(t => (ok: !t.IsFaulted, ex: t.IsFaulted ? t.Exception!.InnerException : null)),
                taskB.ContinueWith(t => (ok: !t.IsFaulted, ex: t.IsFaulted ? t.Exception!.InnerException : null)));

            Assert.Single(results, r => r.ok);
            var failure = Assert.Single(results, r => !r.ok);
            Assert.IsType<PublishStatusConflictException>(failure.ex);

            using var conn = await _factory.CreateOpenConnectionAsync();
            var rowCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PublishStatus WHERE pkid = @Pkid", new { Pkid = pkid });
            Assert.Equal(1, rowCount);
        }
        finally
        {
            await CleanupAsync(pkid);
        }
    }
}
