using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

public class PublishStatusesControllerTests
{
    private readonly Mock<IPublishStatusRepository> _repo = new(MockBehavior.Strict);

    private PublishStatusesController CreateController() => new(_repo.Object);

    private static PublishStatus SampleStatus(byte pkid = 1) => new()
    {
        Pkid = pkid,
        Description = "草稿",
        IsDraft = true,
        IsPublished = false,
        IsDiscontinued = false
    };

    // ---- List ----

    [Fact]
    public async Task GetAll_ReturnsOkWithStatuses()
    {
        var statuses = new List<PublishStatus> { SampleStatus(1), SampleStatus(2) };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(statuses);

        var result = await CreateController().GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<PublishStatus>>(ok.Value);
        Assert.Equal(2, payload.Count());
    }

    // ---- Filter ----

    [Fact]
    public async Task Query_PassesQueryToRepository_ReturnsOk()
    {
        var query = new PublishStatusQuery { Keyword = "草", IsPublished = false };
        var filtered = new List<PublishStatus> { SampleStatus(1) };
        _repo.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(filtered);

        var result = await CreateController().Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<PublishStatus>>(ok.Value);
        Assert.Single(payload);
        _repo.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_NullBody_UsesEmptyQuery()
    {
        _repo.Setup(r => r.QueryAsync(It.IsAny<PublishStatusQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PublishStatus>());

        var result = await CreateController().Query(null!, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _repo.Verify(r => r.QueryAsync(It.IsAny<PublishStatusQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- View ----

    [Fact]
    public async Task GetById_Found_ReturnsOk()
    {
        _repo.Setup(r => r.GetByIdAsync((byte)1, It.IsAny<CancellationToken>())).ReturnsAsync(SampleStatus(1));

        var result = await CreateController().GetById(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var status = Assert.IsType<PublishStatus>(ok.Value);
        Assert.Equal((byte)1, status.Pkid);
        Assert.True(status.IsDraft);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _repo.Setup(r => r.GetByIdAsync((byte)99, It.IsAny<CancellationToken>())).ReturnsAsync((PublishStatus?)null);

        var result = await CreateController().GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- Add ----

    [Fact]
    public async Task Create_New_ReturnsCreatedAtAction()
    {
        var request = new PublishStatusRequest
        {
            Pkid = 3,
            Description = "已發布",
            IsDraft = false,
            IsPublished = true,
            IsDiscontinued = false
        };
        var created = new PublishStatus
        {
            Pkid = 3,
            Description = "已發布",
            IsDraft = false,
            IsPublished = true,
            IsDiscontinued = false
        };

        _repo.Setup(r => r.ExistsAsync((byte)3, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var result = await CreateController().Create(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(PublishStatusesController.GetById), createdResult.ActionName);
        Assert.Equal((byte)3, createdResult.RouteValues!["id"]);
        var body = Assert.IsType<PublishStatus>(createdResult.Value);
        Assert.Equal((byte)3, body.Pkid);
    }

    [Fact]
    public async Task Create_Duplicate_ReturnsConflict()
    {
        var request = new PublishStatusRequest { Pkid = 1, Description = "草稿" };
        _repo.Setup(r => r.ExistsAsync((byte)1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        _repo.Verify(r => r.CreateAsync(It.IsAny<PublishStatusRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_MissingPkid_ReturnsValidationProblem()
    {
        var request = new PublishStatusRequest { Pkid = 0, Description = "草稿" };

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result); // ValidationProblem -> ObjectResult (ProblemDetails)
        _repo.Verify(r => r.ExistsAsync(It.IsAny<byte>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_MissingDescription_ReturnsValidationProblem()
    {
        var request = new PublishStatusRequest { Pkid = 5, Description = "  " };

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        _repo.Verify(r => r.ExistsAsync(It.IsAny<byte>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Edit ----

    [Fact]
    public async Task Update_Existing_ReturnsNoContent()
    {
        var request = new PublishStatusRequest
        {
            Pkid = 1,
            Description = "草稿（更新）",
            IsDraft = true
        };
        _repo.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound()
    {
        var request = new PublishStatusRequest { Pkid = 99, Description = "ghost" };
        _repo.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_MissingDescription_ReturnsValidationProblem()
    {
        var request = new PublishStatusRequest { Pkid = 1, Description = "" };

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<PublishStatusRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_Existing_ReturnsNoContent()
    {
        _repo.Setup(r => r.DeleteAsync((byte)1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Delete(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_Missing_ReturnsNotFound()
    {
        _repo.Setup(r => r.DeleteAsync((byte)99, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController().Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
