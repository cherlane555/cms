using System.Data;
using System.Reflection;
using System.Security.Claims;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Auditing;

/// <summary>
/// Default <see cref="IRowAuditWriter"/> — builds the audit row by reflection over the
/// entity (pkid, first string property, changed-property diff) and inserts it with Dapper.
/// UserName comes from the current request's JWT ("userName" claim), falling back to
/// "system" when there is no authenticated user.
/// </summary>
public class RowAuditWriter : IRowAuditWriter
{
    /// <summary>RowAudit.ActionDesc is varchar(1000) — longer values are truncated.</summary>
    public const int ActionDescMaxLength = 1000;

    private const string FallbackUserName = "system";

    private readonly IDbConnectionFactory _factory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RowAuditWriter(IDbConnectionFactory factory, IHttpContextAccessor httpContextAccessor)
    {
        _factory = factory;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task LogInsertAsync(string tableName, object entity,
        IDbConnection? connection = null, IDbTransaction? transaction = null, CancellationToken ct = default)
        => InsertRowAsync(BuildRow(tableName, "Insert", entity, FirstStringPropertyValue(entity)), connection, transaction, ct);

    public Task LogUpdateAsync(string tableName, object before, object after,
        IDbConnection? connection = null, IDbTransaction? transaction = null, CancellationToken ct = default)
        => InsertRowAsync(BuildRow(tableName, "Update", after, ChangedPropertyNames(before, after)), connection, transaction, ct);

    public Task LogDeleteAsync(string tableName, object entity,
        IDbConnection? connection = null, IDbTransaction? transaction = null, CancellationToken ct = default)
        => InsertRowAsync(BuildRow(tableName, "Delete", entity, FirstStringPropertyValue(entity)), connection, transaction, ct);

    private RowAudit BuildRow(string tableName, string actionType, object entity, string? actionDesc)
    {
        return new RowAudit
        {
            TableName = tableName,
            UserName = ResolveUserName(),
            PrimaryKeyValues = PkidValue(entity),
            ActionType = actionType,
            ActionDesc = Truncate(actionDesc),
            DateTime = DateTime.Now
        };
    }

    /// <summary>Performs the Dapper insert; overridable so unit tests can capture the row.</summary>
    protected virtual async Task InsertRowAsync(RowAudit row, IDbConnection? connection, IDbTransaction? transaction, CancellationToken ct)
    {
        if (connection is not null)
        {
            // Caller-owned connection/transaction: the audit row commits or rolls back with the change.
            await ExecuteInsertAsync(connection, transaction, row, ct);
            return;
        }

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await ExecuteInsertAsync(conn, null, row, ct);
    }

    private static Task<int> ExecuteInsertAsync(IDbConnection conn, IDbTransaction? tx, RowAudit row, CancellationToken ct)
        => conn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO RowAudit (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime])
VALUES (@TableName, @UserName, @PrimaryKeyValues, @ActionType, @ActionDesc, @DateTime);",
            new
            {
                row.TableName,
                row.UserName,
                row.PrimaryKeyValues,
                row.ActionType,
                row.ActionDesc,
                row.DateTime
            }, tx, cancellationToken: ct));

    private string ResolveUserName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return FallbackUserName;
        }

        return user.FindFirstValue("userName")
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? FallbackUserName;
    }

    /// <summary>The entity's pkid property value (case-insensitive lookup), as a string.</summary>
    internal static string PkidValue(object entity)
    {
        var prop = ReadableProperties(entity).FirstOrDefault(p =>
            string.Equals(p.Name, "pkid", StringComparison.OrdinalIgnoreCase));
        return prop?.GetValue(entity)?.ToString() ?? string.Empty;
    }

    /// <summary>The value of the entity's first string-typed property, in declaration order.</summary>
    internal static string FirstStringPropertyValue(object entity)
    {
        var prop = ReadableProperties(entity).FirstOrDefault(p => p.PropertyType == typeof(string));
        return prop?.GetValue(entity) as string ?? string.Empty;
    }

    /// <summary>Comma-separated names of the properties whose value differs between the two entities.</summary>
    internal static string ChangedPropertyNames(object before, object after)
    {
        var changed = ReadableProperties(before)
            .Where(p => !Equals(p.GetValue(before), p.GetValue(after)))
            .Select(p => p.Name);
        return string.Join(",", changed);
    }

    private static IEnumerable<PropertyInfo> ReadableProperties(object entity)
        => entity.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

    private static string? Truncate(string? value)
        => value is { Length: > ActionDescMaxLength } ? value[..ActionDescMaxLength] : value;
}
