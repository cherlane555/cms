using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

public class FeaturedPromoItemsControllerTests
{
    private readonly Mock<IFeaturedPromoItemRepository> _repo = new(MockBehavior.Strict);

    private FeaturedPromoItemsController CreateController() => new(_repo.Object);

    private static FeaturedPromoItem SampleItem(int pkid = 1, byte slot = 1) => new()
    {
        Pkid = pkid,
        ScheduleOn = new DateTime(2026, 7, 13),
        TrainingCenterPkid = 1,
        Slot = slot,
        PromotionPkid = 100,
        Topic = "n8n自動化三部曲",
        Description = "從自動化新手到企業級AI架構師學習路徑",
        PromoCode = "20251215_n8n"
    };

    private static FeaturedPromoItemRequest SampleRequest(int pkid = 0, byte slot = 1) => new()
    {
        Pkid = pkid,
        ScheduleOn = new DateTime(2026, 7, 13),
        TrainingCenterPkid = 1,
        Slot = slot,
        PromotionPkid = 100,
        Topic = "n8n自動化三部曲",
        Description = "從自動化新手到企業級AI架構師學習路徑"
    };

    // ---- Query: one-week ScheduleOn window + TrainingCenter filter ----

    [Fact]
    public async Task Query_ForwardsWeekWindowAndTrainingCenterFilter_ReturnsOk()
    {
        var monday = new DateTime(2026, 7, 13);
        var sunday = new DateTime(2026, 7, 19);
        var query = new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 1,
            ScheduleFrom = monday,
            ScheduleTo = sunday
        };
        var filtered = new List<FeaturedPromoItem> { SampleItem(1), SampleItem(2, 2) };

        _repo.Setup(r => r.QueryAsync(
                It.Is<FeaturedPromoItemQuery>(q =>
                    q.TrainingCenterPkid == 1 && q.ScheduleFrom == monday && q.ScheduleTo == sunday),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(filtered);

        var result = await CreateController().Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<FeaturedPromoItem>>(ok.Value);
        Assert.Equal(2, payload.Count());
        Assert.All(payload, i => Assert.InRange(i.ScheduleOn, monday, sunday));
        _repo.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_TrainingCenterOnly_PassesFilterThrough()
    {
        var query = new FeaturedPromoItemQuery { TrainingCenterPkid = 3 };
        _repo.Setup(r => r.QueryAsync(
                It.Is<FeaturedPromoItemQuery>(q => q.TrainingCenterPkid == 3 && q.ScheduleFrom == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeaturedPromoItem>());

        var result = await CreateController().Query(query, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _repo.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_NullBody_UsesEmptyQuery()
    {
        _repo.Setup(r => r.QueryAsync(It.IsAny<FeaturedPromoItemQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeaturedPromoItem>());

        var result = await CreateController().Query(null!, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _repo.Verify(r => r.QueryAsync(It.IsAny<FeaturedPromoItemQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- View ----

    [Fact]
    public async Task GetById_Found_ReturnsOkWithPromoCode()
    {
        _repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(SampleItem(1));

        var result = await CreateController().GetById(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var item = Assert.IsType<FeaturedPromoItem>(ok.Value);
        Assert.Equal("20251215_n8n", item.PromoCode);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((FeaturedPromoItem?)null);

        var result = await CreateController().GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- Add ----

    [Fact]
    public async Task Create_FreeSlot_ReturnsCreatedAtAction()
    {
        var request = SampleRequest();
        _repo.Setup(r => r.IsSlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(SampleItem(5));

        var result = await CreateController().Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(FeaturedPromoItemsController.GetById), created.ActionName);
        Assert.Equal(5, created.RouteValues!["id"]);
        var body = Assert.IsType<FeaturedPromoItem>(created.Value);
        Assert.Equal(5, body.Pkid);
    }

    [Fact]
    public async Task Create_TakenSlot_ReturnsConflict()
    {
        var request = SampleRequest();
        _repo.Setup(r => r.IsSlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        _repo.Verify(r => r.CreateAsync(It.IsAny<FeaturedPromoItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_MissingPromotion_ReturnsValidationProblem()
    {
        var request = SampleRequest();
        request.PromotionPkid = 0;

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result); // ValidationProblem -> ObjectResult (ProblemDetails)
        _repo.Verify(r => r.CreateAsync(It.IsAny<FeaturedPromoItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Create_SlotOutOfRange_ReturnsValidationProblem(byte slot)
    {
        var request = SampleRequest(slot: slot);

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        _repo.Verify(r => r.CreateAsync(It.IsAny<FeaturedPromoItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_MissingTopic_ReturnsValidationProblem()
    {
        var request = SampleRequest();
        request.Topic = "  ";

        var result = await CreateController().Create(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        _repo.Verify(r => r.CreateAsync(It.IsAny<FeaturedPromoItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Edit ----

    [Fact]
    public async Task Update_Existing_ReturnsNoContent()
    {
        var request = SampleRequest(pkid: 1);
        _repo.Setup(r => r.IsSlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound()
    {
        var request = SampleRequest(pkid: 99);
        _repo.Setup(r => r.IsSlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_SlotTakenByOther_ReturnsConflict()
    {
        var request = SampleRequest(pkid: 1);
        _repo.Setup(r => r.IsSlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<FeaturedPromoItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_MissingPkid_ReturnsValidationProblem()
    {
        var request = SampleRequest(pkid: 0);

        var result = await CreateController().Update(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<FeaturedPromoItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_Existing_ReturnsNoContent()
    {
        _repo.Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateController().Delete(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_Missing_ReturnsNotFound()
    {
        _repo.Setup(r => r.DeleteAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController().Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---- Move slot (+ = down, - = up) ----

    [Fact]
    public async Task Move_Down_CallsRepositoryWithPlusOne()
    {
        _repo.Setup(r => r.MoveSlotAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(MoveSlotResult.Moved);

        var result = await CreateController().Move(1, new MoveSlotRequest { Direction = "down" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _repo.Verify(r => r.MoveSlotAsync(1, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Move_Up_CallsRepositoryWithMinusOne()
    {
        _repo.Setup(r => r.MoveSlotAsync(2, -1, It.IsAny<CancellationToken>())).ReturnsAsync(MoveSlotResult.Moved);

        var result = await CreateController().Move(2, new MoveSlotRequest { Direction = "up" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _repo.Verify(r => r.MoveSlotAsync(2, -1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Move_MissingItem_ReturnsNotFound()
    {
        _repo.Setup(r => r.MoveSlotAsync(99, 1, It.IsAny<CancellationToken>())).ReturnsAsync(MoveSlotResult.NotFound);

        var result = await CreateController().Move(99, new MoveSlotRequest { Direction = "down" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Move_OutOfRange_ReturnsConflict()
    {
        _repo.Setup(r => r.MoveSlotAsync(1, -1, It.IsAny<CancellationToken>())).ReturnsAsync(MoveSlotResult.OutOfRange);

        var result = await CreateController().Move(1, new MoveSlotRequest { Direction = "up" }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Move_InvalidDirection_ReturnsValidationProblem()
    {
        var result = await CreateController().Move(1, new MoveSlotRequest { Direction = "sideways" }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        _repo.Verify(r => r.MoveSlotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
