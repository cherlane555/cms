using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

public class RowAuditControllerTests
{
    private readonly Mock<IRowAuditRepository> _repo = new(MockBehavior.Strict);

    private RowAuditController CreateController() => new(_repo.Object);

    [Fact]
    public async Task Get_PassesTableNameAndPkidToRepository_ReturnsOk()
    {
        var entries = new List<RowAuditEntry>
        {
            new() { DateTime = new DateTime(2026, 6, 4, 14, 30, 0), UserName = "alice", ActionType = "Update", ActionDesc = "Title" },
            new() { DateTime = new DateTime(2026, 6, 1, 9, 0, 0), UserName = "bob", ActionType = "Insert", ActionDesc = "課程A" },
        };
        _repo.Setup(r => r.GetForRecordAsync("Course", "123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await CreateController().GetForRecord("Course", "123", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<RowAuditEntry>>(ok.Value);
        Assert.Equal(2, payload.Count());
        _repo.Verify(r => r.GetForRecordAsync("Course", "123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_TrimsQueryParameters()
    {
        _repo.Setup(r => r.GetForRecordAsync("Course", "123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RowAuditEntry>());

        var result = await CreateController().GetForRecord(" Course ", " 123 ", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _repo.Verify(r => r.GetForRecordAsync("Course", "123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null, "123")]
    [InlineData("", "123")]
    [InlineData("Course", null)]
    [InlineData("Course", " ")]
    public async Task Get_MissingTableNameOrPkid_ReturnsValidationProblem(string? tableName, string? pkid)
    {
        var result = await CreateController().GetForRecord(tableName, pkid, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result); // ValidationProblem -> ObjectResult (ProblemDetails)
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        _repo.Verify(r => r.GetForRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
