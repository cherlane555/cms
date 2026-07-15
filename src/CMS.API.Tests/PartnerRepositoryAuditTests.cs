using System.Security.Claims;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Repositories;
using Dapper;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests;

/// <summary>
/// Integration tests against the local CMS database proving the retrofitted
/// PartnerRepository writes the right RowAudit row on each path (and none on failure).
/// Every test creates its own Partner row and cleans up both it and its audit rows.
/// </summary>
public class PartnerRepositoryAuditTests
{
    private const string ConnString =
        "Server=.\\SQLEXPRESS;Database=CMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private const string AuditUser = "audit-tester";

    private readonly SqlConnectionFactory _factory = new(ConnString);

    private PartnerRepository CreateRepo()
    {
        var identity = new ClaimsIdentity(new[] { new Claim("userName", AuditUser) }, authenticationType: "Test");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return new PartnerRepository(_factory, new RowAuditWriter(_factory, accessor));
    }

    private static PartnerRequest NewRequest(string? name = null) => new()
    {
        Name = name ?? $"AT-{Guid.NewGuid().ToString("N")[..8]}",
        AppKey = $"at{Guid.NewGuid().ToString("N")[..6]}",
        NameOnPartnerMenu = "audit menu",
        NameOnCourseDetailPage = "audit detail",
        DisplayOrder = 9999,
        ImageFilename = null
    };

    private async Task<List<RowAudit>> GetAuditRowsAsync(string primaryKeyValues)
    {
        using var conn = await _factory.CreateOpenConnectionAsync();
        var rows = await conn.QueryAsync<RowAudit>(@"
SELECT pkid, TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime]
FROM RowAudit
WHERE TableName = 'Partner' AND PrimaryKeyValues = @Pk
ORDER BY pkid ASC",
            new { Pk = primaryKeyValues });
        return rows.ToList();
    }

    /// <summary>Raw-SQL cleanup (not via the repository, so it writes no extra audit rows).</summary>
    private async Task CleanupAsync(short pkid)
    {
        using var conn = await _factory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(@"
DELETE FROM Partner WHERE pkid = @Pkid;
DELETE FROM RowAudit WHERE TableName = 'Partner' AND PrimaryKeyValues = @Pk;",
            new { Pkid = pkid, Pk = pkid.ToString() });
    }

    [Fact]
    public async Task Create_WritesInsertAuditRow_WithFirstStringColumn()
    {
        var repo = CreateRepo();
        var request = NewRequest();
        var created = await repo.CreateAsync(request);
        try
        {
            var rows = await GetAuditRowsAsync(created.Pkid.ToString());

            var row = Assert.Single(rows);
            Assert.Equal("Insert", row.ActionType);
            Assert.Equal(request.Name, row.ActionDesc); // Name is Partner's first string column
            Assert.Equal(AuditUser, row.UserName);
            Assert.Equal(created.Pkid.ToString(), row.PrimaryKeyValues);
        }
        finally
        {
            await CleanupAsync(created.Pkid);
        }
    }

    [Fact]
    public async Task Update_WritesUpdateAuditRow_ListingExactlyTheChangedColumns()
    {
        var repo = CreateRepo();
        var request = NewRequest();
        var created = await repo.CreateAsync(request);
        try
        {
            // Change exactly two columns: Name and DisplayOrder.
            request.Pkid = created.Pkid;
            request.Name = $"AT-{Guid.NewGuid().ToString("N")[..8]}";
            request.DisplayOrder = 9998;

            var updated = await repo.UpdateAsync(request);

            Assert.True(updated);
            var rows = await GetAuditRowsAsync(created.Pkid.ToString());
            var updateRow = Assert.Single(rows, r => r.ActionType == "Update");
            Assert.Equal("Name,DisplayOrder", updateRow.ActionDesc);
            Assert.Equal(AuditUser, updateRow.UserName);
        }
        finally
        {
            await CleanupAsync(created.Pkid);
        }
    }

    [Fact]
    public async Task Update_WithNoChanges_WritesEmptyActionDesc()
    {
        var repo = CreateRepo();
        var request = NewRequest();
        var created = await repo.CreateAsync(request);
        try
        {
            request.Pkid = created.Pkid; // resave identical values

            var updated = await repo.UpdateAsync(request);

            Assert.True(updated);
            var rows = await GetAuditRowsAsync(created.Pkid.ToString());
            var updateRow = Assert.Single(rows, r => r.ActionType == "Update");
            Assert.Equal(string.Empty, updateRow.ActionDesc);
        }
        finally
        {
            await CleanupAsync(created.Pkid);
        }
    }

    [Fact]
    public async Task Delete_WritesDeleteAuditRow_WithFirstStringColumn()
    {
        var repo = CreateRepo();
        var request = NewRequest();
        var created = await repo.CreateAsync(request);
        try
        {
            var deleted = await repo.DeleteAsync(created.Pkid);

            Assert.True(deleted);
            var rows = await GetAuditRowsAsync(created.Pkid.ToString());
            var deleteRow = Assert.Single(rows, r => r.ActionType == "Delete");
            Assert.Equal(request.Name, deleteRow.ActionDesc);
            Assert.Equal(AuditUser, deleteRow.UserName);
        }
        finally
        {
            await CleanupAsync(created.Pkid);
        }
    }

    [Fact]
    public async Task FailedUpdate_LeavesNoAuditRow()
    {
        var repo = CreateRepo();
        var request = NewRequest();
        request.Pkid = -1; // no such row

        var updated = await repo.UpdateAsync(request);

        Assert.False(updated);
        Assert.Empty(await GetAuditRowsAsync("-1"));
    }

    [Fact]
    public async Task FailedDelete_LeavesNoAuditRow()
    {
        var repo = CreateRepo();

        var deleted = await repo.DeleteAsync(-1);

        Assert.False(deleted);
        Assert.Empty(await GetAuditRowsAsync("-1"));
    }
}
