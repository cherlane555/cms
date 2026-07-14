namespace CMS.API.Models;

/// <summary>
/// PublishStatus response model. Maps to dbo.PublishStatus — a lookup table of content
/// publishing states.
/// The primary key <see cref="Pkid"/> is a <c>tinyint</c> that is <b>user-assigned</b>
/// (NOT an IDENTITY column), so it is supplied by the caller on create.
/// </summary>
public class PublishStatus
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}
