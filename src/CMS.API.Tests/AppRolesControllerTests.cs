using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

public class AppRolesControllerTests
{
    private readonly Mock<IAppRoleRepository> _repo = new(MockBehavior.Strict);

    private AppRolesController CreateController() => new(_repo.Object);

    private static AppRole SampleRole(string id = "Admin") => new()
    {
        Pkid = 1,
        RoleId = id,
        RoleName = "Administrator",
        PermissionLevel = 1,
        Description = "系統管理員",
        UserCount = 3,
        UserIds = new List<string> { "helen", "Jenny_Tsao", "miles@uuu.com.tw" }
    };

    // ---- List ----

    [Fact]
    public async Task GetAll_ReturnsOkWithRoles()
    {
        var roles = new List<AppRole> { SampleRole("Admin"), SampleRole("User") };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(roles);

        var result = await CreateController().GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<AppRole>>(ok.Value);
        Assert.Equal(2, payload.Count());
    }

    // ---- Filter ----

    [Fact]
    public async Task Query_PassesQueryToRepository_ReturnsOk()
    {
        var query = new AppRoleQuery { Keyword = "adm", PermissionLevel = 1 };
        var filtered = new List<AppRole> { SampleRole("Admin") };
        _repo.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(filtered);

        var result = await CreateController().Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<AppRole>>(ok.Value);
        Assert.Single(payload);
        _repo.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_NullBody_UsesEmptyQuery()
    {
        _repo.Setup(r => r.QueryAsync(It.IsAny<AppRoleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppRole>());

        var result = await CreateController().Query(null!, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _repo.Verify(r => r.QueryAsync(It.IsAny<AppRoleQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- View ----

    [Fact]
    public async Task GetById_Found_ReturnsOkWithUserIds()
    {
        _repo.Setup(r => r.GetByIdAsync("Admin", It.IsAny<CancellationToken>())).ReturnsAsync(SampleRole("Admin"));

        var result = await CreateController().GetById("Admin", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var role = Assert.IsType<AppRole>(ok.Value);
        Assert.Equal("Admin", role.RoleId);
        Assert.Equal(3, role.UserIds.Count);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _repo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((AppRole?)null);

        var result = await CreateController().GetById("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- Add ----

    [Fact]
    public async Task Create_New_ReturnsCreatedAtAction()
    {
        var request = new AppRoleRequest
        {
            RoleId = "Editor",
            RoleName = "Editor",
            PermissionLevel = 50,
            Description = "編輯",
            UserIds = new List<string> { "helen" }
        };
        var created = new AppRole { Pkid = 3, RoleId = "Editor", RoleName = "Editor", PermissionLevel = 50 };

        _repo.Setup(r => r.ExistsAsync("Editor", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var result = await CreateController().Create(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(AppRolesController.GetById), createdResult.ActionName);
        Assert.Equal("Editor", createdResult.RouteValues!["id"]);
        var body = Assert.IsType<AppRole>(createdResult.Value);
        Assert.Equal(3, body.Pkid);
    }

    [Fact]
    public async Task Create_Duplicate_ReturnsConflict()
    {
        var request = new AppRoleRequest { RoleId = "Admin", RoleName = "Administrator", PermissionLevel = 1 };
        _repo.Setup(r => r.ExistsAsync("Admin", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        _repo.Verify(r => r.CreateAsync(It.IsAny<AppRoleRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_MissingRoleId_ReturnsValidationProblem()
    {
        var request = new AppRoleRequest { RoleId = "", RoleName = "x", PermissionLevel = 1 };

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result); // ValidationProblem -> ObjectResult (ProblemDetails)
        _repo.Verify(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Edit ----

    [Fact]
    public async Task Update_Existing_ReturnsNoContent()
    {
        var request = new AppRoleRequest
        {
            RoleId = "Admin",
            RoleName = "Administrator (updated)",
            PermissionLevel = 1,
            Description = "系統管理員",
            UserIds = new List<string> { "helen", "miles@uuu.com.tw" }
        };
        _repo.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound()
    {
        var request = new AppRoleRequest { RoleId = "ghost", RoleName = "x", PermissionLevel = 1 };
        _repo.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_MissingRoleId_ReturnsValidationProblem()
    {
        var request = new AppRoleRequest { RoleId = "  ", RoleName = "x", PermissionLevel = 1 };

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<AppRoleRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_Existing_ReturnsNoContent()
    {
        _repo.Setup(r => r.DeleteAsync("Admin", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Delete("Admin", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_Missing_ReturnsNotFound()
    {
        _repo.Setup(r => r.DeleteAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController().Delete("ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
