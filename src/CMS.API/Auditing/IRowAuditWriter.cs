using System.Data;

namespace CMS.API.Auditing;

/// <summary>
/// Cross-cutting row-audit writer. Repositories call it after an Insert / Update / Delete
/// to record one RowAudit row describing the change. Generic: works for any entity type
/// via reflection.
/// Pass the repository's open connection + transaction so the audit row commits (or rolls
/// back) atomically with the change itself; with no connection the writer opens its own.
/// </summary>
public interface IRowAuditWriter
{
    /// <summary>Logs an Insert; ActionDesc is the entity's first string property value.</summary>
    Task LogInsertAsync(string tableName, object entity,
        IDbConnection? connection = null, IDbTransaction? transaction = null, CancellationToken ct = default);

    /// <summary>Logs an Update; ActionDesc is a comma-separated list of the changed property names.</summary>
    Task LogUpdateAsync(string tableName, object before, object after,
        IDbConnection? connection = null, IDbTransaction? transaction = null, CancellationToken ct = default);

    /// <summary>Logs a Delete; ActionDesc is the entity's first string property value.</summary>
    Task LogDeleteAsync(string tableName, object entity,
        IDbConnection? connection = null, IDbTransaction? transaction = null, CancellationToken ct = default);
}
