using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests;

public class LookupsControllerTests
{
    private readonly Mock<ILookupRepository> _repo = new(MockBehavior.Strict);

    private LookupsController CreateController() => new(_repo.Object);

    // ---- TrainingCenter tabs ----

    [Fact]
    public async Task GetTrainingCenters_ReturnsOkWithCenters()
    {
        var centers = new List<TrainingCenterLookup>
        {
            new() { Pkid = 1, Name = "台北" },
            new() { Pkid = 2, Name = "新竹" },
            new() { Pkid = 5, Name = "線上研討會" }
        };
        _repo.Setup(r => r.GetTrainingCentersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(centers);

        var result = await CreateController().GetTrainingCenters(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<TrainingCenterLookup>>(ok.Value);
        Assert.Equal(3, payload.Count());
        Assert.Equal("台北", payload.First().Name);
    }

    // ---- PromoCode -> Promotion_pkid lookup ----

    [Fact]
    public async Task GetPromotionByCode_Found_ReturnsOkWithPkid()
    {
        var promotion = new PromotionLookup
        {
            Pkid = 100,
            PromoCode = "20251215_n8n",
            Topic = "n8n自動化三部曲",
            Description = "從自動化新手到企業級AI架構師學習路徑"
        };
        _repo.Setup(r => r.GetPromotionByCodeAsync("20251215_n8n", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promotion);

        var result = await CreateController().GetPromotionByCode("20251215_n8n", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PromotionLookup>(ok.Value);
        Assert.Equal(100, payload.Pkid);
        Assert.Equal("n8n自動化三部曲", payload.Topic);
    }

    [Fact]
    public async Task GetPromotionByCode_Unknown_Returns404()
    {
        _repo.Setup(r => r.GetPromotionByCodeAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromotionLookup?)null);

        var result = await CreateController().GetPromotionByCode("nope", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
