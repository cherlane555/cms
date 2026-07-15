using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Read side of the RowAudit table: one record's audit trail.</summary>
public interface IRowAuditRepository
{
    /// <summary>Audit rows for one record (TableName + pkid), newest first.</summary>
    Task<IEnumerable<RowAuditEntry>> GetForRecordAsync(string tableName, string pkid, CancellationToken ct = default);
}
