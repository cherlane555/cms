using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/rowaudit")]
public class RowAuditController : ControllerBase
{
    private readonly IRowAuditRepository _repository;

    public RowAuditController(IRowAuditRepository repository)
    {
        _repository = repository;
    }

    /// <summary>One record's audit trail (by TableName + pkid), newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RowAuditEntry>>> GetForRecord(
        [FromQuery] string? tableName, [FromQuery] string? pkid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            ModelState.AddModelError(nameof(tableName), "tableName is required.");
        }
        if (string.IsNullOrWhiteSpace(pkid))
        {
            ModelState.AddModelError(nameof(pkid), "pkid is required.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var entries = await _repository.GetForRecordAsync(tableName!.Trim(), pkid!.Trim(), ct);
        return Ok(entries);
    }
}
