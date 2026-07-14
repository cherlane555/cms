using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

public class PartnersControllerTests
{
    private readonly Mock<IPartnerRepository> _repo = new(MockBehavior.Strict);

    private PartnersController CreateController() => new(_repo.Object);

    private static Partner SamplePartner(short pkid = 1) => new()
    {
        Pkid = pkid,
        Name = "恆逸資訊",
        AppKey = "uwa",
        NameOnPartnerMenu = "恆逸資訊教育訓練中心",
        NameOnCourseDetailPage = "恆逸",
        DisplayOrder = 10,
        ImageFilename = "uwa.png"
    };

    // ---- List ----

    [Fact]
    public async Task GetAll_ReturnsOkWithPartners()
    {
        var partners = new List<Partner> { SamplePartner(1), SamplePartner(2) };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(partners);

        var result = await CreateController().GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<Partner>>(ok.Value);
        Assert.Equal(2, payload.Count());
    }

    // ---- Filter ----

    [Fact]
    public async Task Query_PassesQueryToRepository_ReturnsOk()
    {
        var query = new PartnerQuery { Keyword = "恆逸" };
        var filtered = new List<Partner> { SamplePartner(1) };
        _repo.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(filtered);

        var result = await CreateController().Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<Partner>>(ok.Value);
        Assert.Single(payload);
        _repo.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_NullBody_UsesEmptyQuery()
    {
        _repo.Setup(r => r.QueryAsync(It.IsAny<PartnerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Partner>());

        var result = await CreateController().Query(null!, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _repo.Verify(r => r.QueryAsync(It.IsAny<PartnerQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- View ----

    [Fact]
    public async Task GetById_Found_ReturnsOk()
    {
        _repo.Setup(r => r.GetByIdAsync((short)1, It.IsAny<CancellationToken>())).ReturnsAsync(SamplePartner(1));

        var result = await CreateController().GetById(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var partner = Assert.IsType<Partner>(ok.Value);
        Assert.Equal((short)1, partner.Pkid);
        Assert.Equal("恆逸資訊", partner.Name);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _repo.Setup(r => r.GetByIdAsync((short)99, It.IsAny<CancellationToken>())).ReturnsAsync((Partner?)null);

        var result = await CreateController().GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- Add ----

    [Fact]
    public async Task Create_New_ReturnsCreatedAtAction()
    {
        var request = new PartnerRequest
        {
            Name = "新廠商",
            AppKey = "new",
            NameOnPartnerMenu = "新廠商選單名",
            NameOnCourseDetailPage = "新廠商",
            DisplayOrder = 20
        };
        var created = SamplePartner(5);
        created.Name = "新廠商";

        _repo.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var result = await CreateController().Create(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(PartnersController.GetById), createdResult.ActionName);
        Assert.Equal((short)5, createdResult.RouteValues!["id"]);
        var body = Assert.IsType<Partner>(createdResult.Value);
        Assert.Equal((short)5, body.Pkid);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsValidationProblem()
    {
        var request = new PartnerRequest { Name = "", AppKey = "x" };

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result); // ValidationProblem -> ObjectResult (ProblemDetails)
        _repo.Verify(r => r.CreateAsync(It.IsAny<PartnerRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_MissingAppKey_ReturnsValidationProblem()
    {
        var request = new PartnerRequest { Name = "有名稱", AppKey = "  " };

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        _repo.Verify(r => r.CreateAsync(It.IsAny<PartnerRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Edit ----

    [Fact]
    public async Task Update_Existing_ReturnsNoContent()
    {
        var request = new PartnerRequest
        {
            Pkid = 1,
            Name = "恆逸資訊（更新）",
            AppKey = "uwa",
            NameOnPartnerMenu = "選單名",
            NameOnCourseDetailPage = "恆逸",
            DisplayOrder = 10
        };
        _repo.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound()
    {
        var request = new PartnerRequest { Pkid = 99, Name = "ghost", AppKey = "g" };
        _repo.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_MissingPkid_ReturnsValidationProblem()
    {
        var request = new PartnerRequest { Pkid = 0, Name = "有名稱", AppKey = "x" };

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<PartnerRequest>(), It.IsAny<CancellationToken>()), Times.Never);
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
