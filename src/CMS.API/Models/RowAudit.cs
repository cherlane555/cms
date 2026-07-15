namespace CMS.API.Models;

/// <summary>One row of the RowAudit table — describes a change to any business table.</summary>
public class RowAudit
{
    public int Pkid { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string PrimaryKeyValues { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? ActionDesc { get; set; }
    public DateTime DateTime { get; set; }
}
