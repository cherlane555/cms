using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

public class CourseGroupsControllerTests
{
    private readonly Mock<ICourseGroupRepository> _repo = new(MockBehavior.Strict);

    private CourseGroupsController CreateController() => new(_repo.Object);

    private static CourseGroup SampleGroup(short pkid = 1) => new()
    {
        Pkid = pkid,
        Description = "資料庫管理"
    };

    // ---- List ----

    [Fact]
    public async Task GetAll_ReturnsOkWithCourseGroups()
    {
        var groups = new List<CourseGroup> { SampleGroup(1), SampleGroup(2) };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(groups);

        var result = await CreateController().GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<CourseGroup>>(ok.Value);
        Assert.Equal(2, payload.Count());
    }

    // ---- Filter ----

    [Fact]
    public async Task Query_PassesQueryToRepository_ReturnsOk()
    {
        var query = new CourseGroupQuery { Keyword = "資料庫" };
        var filtered = new List<CourseGroup> { SampleGroup(1) };
        _repo.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(filtered);

        var result = await CreateController().Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<CourseGroup>>(ok.Value);
        Assert.Single(payload);
        _repo.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_NullBody_UsesEmptyQuery()
    {
        _repo.Setup(r => r.QueryAsync(It.IsAny<CourseGroupQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CourseGroup>());

        var result = await CreateController().Query(null!, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _repo.Verify(r => r.QueryAsync(It.IsAny<CourseGroupQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- View ----

    [Fact]
    public async Task GetById_Found_ReturnsOk()
    {
        _repo.Setup(r => r.GetByIdAsync((short)1, It.IsAny<CancellationToken>())).ReturnsAsync(SampleGroup(1));

        var result = await CreateController().GetById(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var group = Assert.IsType<CourseGroup>(ok.Value);
        Assert.Equal((short)1, group.Pkid);
        Assert.Equal("資料庫管理", group.Description);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _repo.Setup(r => r.GetByIdAsync((short)99, It.IsAny<CancellationToken>())).ReturnsAsync((CourseGroup?)null);

        var result = await CreateController().GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- Add ----

    [Fact]
    public async Task Create_New_ReturnsCreatedAtAction()
    {
        var request = new CourseGroupRequest { Description = "雲端技術" };
        var created = new CourseGroup { Pkid = 5, Description = "雲端技術" };

        _repo.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var result = await CreateController().Create(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CourseGroupsController.GetById), createdResult.ActionName);
        Assert.Equal((short)5, createdResult.RouteValues!["id"]);
        var body = Assert.IsType<CourseGroup>(createdResult.Value);
        Assert.Equal((short)5, body.Pkid);
    }

    [Fact]
    public async Task Create_MissingDescription_ReturnsValidationProblem()
    {
        var request = new CourseGroupRequest { Description = "" };

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result); // ValidationProblem -> ObjectResult (ProblemDetails)
        _repo.Verify(r => r.CreateAsync(It.IsAny<CourseGroupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhitespaceDescription_ReturnsValidationProblem()
    {
        var request = new CourseGroupRequest { Description = "   " };

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        _repo.Verify(r => r.CreateAsync(It.IsAny<CourseGroupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Edit ----

    [Fact]
    public async Task Update_Existing_ReturnsNoContent()
    {
        var request = new CourseGroupRequest { Pkid = 1, Description = "資料庫管理（更新）" };
        _repo.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound()
    {
        var request = new CourseGroupRequest { Pkid = 99, Description = "ghost" };
        _repo.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_MissingPkid_ReturnsValidationProblem()
    {
        var request = new CourseGroupRequest { Pkid = 0, Description = "有描述" };

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<CourseGroupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_MissingDescription_ReturnsValidationProblem()
    {
        var request = new CourseGroupRequest { Pkid = 1, Description = "" };

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<CourseGroupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_Existing_ReturnsNoContent()
    {
        _repo.Setup(r => r.DeleteAsync((short)1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Delete(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_Missing_ReturnsNotFound()
    {
        _repo.Setup(r => r.DeleteAsync((short)99, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController().Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
