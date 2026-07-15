using System.Security.Claims;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Repositories;
using Dapper;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests;

/// <summary>
/// Spot-check that the audit retrofit fires for a second feature (CourseGroup),
/// not just Partner: the full Insert → Update → Delete lifecycle leaves three audit rows.
/// </summary>
public class CourseGroupRepositoryAuditTests
{
    private const string ConnString =
        "Server=.\\SQLEXPRESS;Database=CMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private readonly SqlConnectionFactory _factory = new(ConnString);

    [Fact]
    public async Task InsertUpdateDelete_EachWritesAnAuditRow()
    {
        var identity = new ClaimsIdentity(new[] { new Claim("userName", "audit-tester") }, authenticationType: "Test");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        var repo = new CourseGroupRepository(_factory, new RowAuditWriter(_factory, accessor));

        var originalDescription = $"AT-{Guid.NewGuid().ToString("N")[..8]}";
        var created = await repo.CreateAsync(new CourseGroupRequest { Description = originalDescription });
        try
        {
            var renamed = $"AT-{Guid.NewGuid().ToString("N")[..8]}";
            Assert.True(await repo.UpdateAsync(new CourseGroupRequest { Pkid = created.Pkid, Description = renamed }));
            Assert.True(await repo.DeleteAsync(created.Pkid));

            using var conn = await _factory.CreateOpenConnectionAsync();
            var rows = (await conn.QueryAsync<RowAudit>(@"
SELECT pkid, TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime]
FROM RowAudit
WHERE TableName = 'CourseGroup' AND PrimaryKeyValues = @Pk
ORDER BY pkid ASC",
                new { Pk = created.Pkid.ToString() })).ToList();

            Assert.Equal(3, rows.Count);
            Assert.Equal("Insert", rows[0].ActionType);
            Assert.Equal(originalDescription, rows[0].ActionDesc);
            Assert.Equal("Update", rows[1].ActionType);
            Assert.Equal("Description", rows[1].ActionDesc);
            Assert.Equal("Delete", rows[2].ActionType);
            Assert.Equal(renamed, rows[2].ActionDesc);
            Assert.All(rows, r => Assert.Equal("audit-tester", r.UserName));
        }
        finally
        {
            using var conn = await _factory.CreateOpenConnectionAsync();
            await conn.ExecuteAsync(@"
DELETE FROM CourseGroup WHERE pkid = @Pkid;
DELETE FROM RowAudit WHERE TableName = 'CourseGroup' AND PrimaryKeyValues = @Pk;",
                new { created.Pkid, Pk = created.Pkid.ToString() });
        }
    }
}
