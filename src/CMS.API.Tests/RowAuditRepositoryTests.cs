using CMS.API.Data;
using CMS.API.Repositories;
using Dapper;

namespace CMS.API.Tests;

/// <summary>
/// Integration tests against the local CMS database: GET-side repository filters by
/// TableName + pkid and returns rows newest first.
/// </summary>
public class RowAuditRepositoryTests
{
    private const string ConnString =
        "Server=.\\SQLEXPRESS;Database=CMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private readonly SqlConnectionFactory _factory = new(ConnString);

    [Fact]
    public async Task GetForRecord_FiltersByTableAndPkid_NewestFirst()
    {
        // A pkid far outside any real table's range so seeded rows can't collide with app data.
        var pk = $"-{Math.Abs(Guid.NewGuid().GetHashCode())}";
        var repo = new RowAuditRepository(_factory);

        using (var conn = await _factory.CreateOpenConnectionAsync())
        {
            await conn.ExecuteAsync(@"
INSERT INTO RowAudit (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime]) VALUES
('AuditTestA', 'alice', @Pk, 'Insert', 'first',  '2026-06-01T09:00:00'),
('AuditTestA', 'bob',   @Pk, 'Update', 'second', '2026-06-03T10:00:00'),
('AuditTestA', 'carol', @Pk, 'Update', 'third',  '2026-06-02T08:00:00'),
('AuditTestB', 'mallory', @Pk, 'Delete', 'other table', '2026-06-04T12:00:00'),
('AuditTestA', 'mallory', '999999999', 'Delete', 'other record', '2026-06-05T12:00:00');",
                new { Pk = pk });
        }

        try
        {
            var rows = (await repo.GetForRecordAsync("AuditTestA", pk)).ToList();

            Assert.Equal(3, rows.Count); // other table / other record filtered out
            Assert.Equal(new[] { "second", "third", "first" }, rows.Select(r => r.ActionDesc));
            Assert.Equal("bob", rows[0].UserName);
            Assert.Equal("Update", rows[0].ActionType);
        }
        finally
        {
            using var conn = await _factory.CreateOpenConnectionAsync();
            await conn.ExecuteAsync(
                "DELETE FROM RowAudit WHERE TableName IN ('AuditTestA', 'AuditTestB')");
        }
    }
}
