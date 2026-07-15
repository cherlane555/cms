using System.Data;
using System.Security.Claims;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CMS.API.Tests;

public class RowAuditWriterTests
{
    /// <summary>Test entity: pkid first (non-string), then two string properties in declaration order.</summary>
    private class Widget
    {
        public int Pkid { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public int Quantity { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>Captures the built RowAudit row instead of inserting it into the database.</summary>
    private sealed class CapturingRowAuditWriter : RowAuditWriter
    {
        public RowAudit? Captured { get; private set; }

        public CapturingRowAuditWriter(IHttpContextAccessor accessor)
            : base(Mock.Of<IDbConnectionFactory>(), accessor)
        {
        }

        protected override Task InsertRowAsync(RowAudit row, IDbConnection? connection, IDbTransaction? transaction, CancellationToken ct)
        {
            Captured = row;
            return Task.CompletedTask;
        }
    }

    private static IHttpContextAccessor AuthenticatedAccessor(string userName)
    {
        var identity = new ClaimsIdentity(new[] { new Claim("userName", userName) }, authenticationType: "Test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextAccessor { HttpContext = context };
    }

    private static IHttpContextAccessor AnonymousAccessor()
        => new HttpContextAccessor { HttpContext = null };

    private static CapturingRowAuditWriter Writer(IHttpContextAccessor? accessor = null)
        => new(accessor ?? AuthenticatedAccessor("alice"));

    [Fact]
    public async Task LogInsert_UsesFirstStringPropertyAsActionDesc()
    {
        var writer = Writer();
        var entity = new Widget { Pkid = 7, Name = "First course", Code = "C-01" };

        await writer.LogInsertAsync("Widget", entity);

        Assert.NotNull(writer.Captured);
        Assert.Equal("Widget", writer.Captured!.TableName);
        Assert.Equal("Insert", writer.Captured.ActionType);
        Assert.Equal("First course", writer.Captured.ActionDesc);
    }

    [Fact]
    public async Task LogDelete_UsesFirstStringPropertyAsActionDesc()
    {
        var writer = Writer();
        var entity = new Widget { Pkid = 3, Name = "Doomed row", Code = "X" };

        await writer.LogDeleteAsync("Widget", entity);

        Assert.Equal("Delete", writer.Captured!.ActionType);
        Assert.Equal("Doomed row", writer.Captured.ActionDesc);
    }

    [Fact]
    public async Task LogUpdate_ListsExactlyTheChangedPropertyNames()
    {
        var writer = Writer();
        var before = new Widget { Pkid = 5, Name = "Old", Code = "K", Quantity = 1, IsActive = true };
        var after = new Widget { Pkid = 5, Name = "New", Code = "K", Quantity = 2, IsActive = true };

        await writer.LogUpdateAsync("Widget", before, after);

        Assert.Equal("Update", writer.Captured!.ActionType);
        Assert.Equal("Name,Quantity", writer.Captured.ActionDesc);
    }

    [Fact]
    public async Task LogUpdate_NothingChanged_WritesEmptyActionDesc()
    {
        var writer = Writer();
        var before = new Widget { Pkid = 5, Name = "Same", Code = "K", Quantity = 1, IsActive = true };
        var after = new Widget { Pkid = 5, Name = "Same", Code = "K", Quantity = 1, IsActive = true };

        await writer.LogUpdateAsync("Widget", before, after);

        Assert.Equal(string.Empty, writer.Captured!.ActionDesc);
    }

    [Fact]
    public async Task PrimaryKeyValues_ReadsPkidAsString()
    {
        var writer = Writer();

        await writer.LogInsertAsync("Widget", new Widget { Pkid = 42, Name = "n" });

        Assert.Equal("42", writer.Captured!.PrimaryKeyValues);
    }

    [Fact]
    public async Task UserName_ComesFromJwtClaim_WhenAuthenticated()
    {
        var writer = Writer(AuthenticatedAccessor("bob"));

        await writer.LogInsertAsync("Widget", new Widget { Pkid = 1, Name = "n" });

        Assert.Equal("bob", writer.Captured!.UserName);
    }

    [Fact]
    public async Task UserName_FallsBackToSystem_WithNoHttpContext()
    {
        var writer = Writer(AnonymousAccessor());

        await writer.LogInsertAsync("Widget", new Widget { Pkid = 1, Name = "n" });

        Assert.Equal("system", writer.Captured!.UserName);
    }

    [Fact]
    public async Task UserName_FallsBackToSystem_WithUnauthenticatedUser()
    {
        // A request with a principal but no authenticated identity (no [Authorize] hit).
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        var writer = Writer(new HttpContextAccessor { HttpContext = context });

        await writer.LogInsertAsync("Widget", new Widget { Pkid = 1, Name = "n" });

        Assert.Equal("system", writer.Captured!.UserName);
    }

    [Fact]
    public async Task ActionDesc_IsTruncatedTo1000Characters()
    {
        var writer = Writer();
        var longName = new string('x', 1500);

        await writer.LogInsertAsync("Widget", new Widget { Pkid = 1, Name = longName });

        Assert.Equal(1000, writer.Captured!.ActionDesc!.Length);
        Assert.Equal(new string('x', 1000), writer.Captured.ActionDesc);
    }
}
