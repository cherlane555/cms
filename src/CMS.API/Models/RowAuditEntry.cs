namespace CMS.API.Models;

/// <summary>One audit-trail entry for a record, as returned by GET /api/rowaudit.</summary>
public class RowAuditEntry
{
    public DateTime DateTime { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? ActionDesc { get; set; }
}
