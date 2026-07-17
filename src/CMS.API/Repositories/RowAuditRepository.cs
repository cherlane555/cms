using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class RowAuditRepository : IRowAuditRepository
{
    private readonly IDbConnectionFactory _factory;

    public RowAuditRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Caps how many audit rows a single record can return — a long-lived record can
    /// accumulate an unbounded number of entries otherwise.</summary>
    private const int MaxEntries = 200;

    public async Task<IEnumerable<RowAuditEntry>> GetForRecordAsync(string tableName, string pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<RowAuditEntry>(new CommandDefinition($@"
SELECT TOP ({MaxEntries}) a.[DateTime], a.UserName, a.ActionType, a.ActionDesc
FROM RowAudit a
WHERE a.TableName = @TableName AND a.PrimaryKeyValues = @Pkid
ORDER BY a.[DateTime] DESC, a.pkid DESC",
            new { TableName = tableName, Pkid = pkid }, cancellationToken: ct));
    }
}
