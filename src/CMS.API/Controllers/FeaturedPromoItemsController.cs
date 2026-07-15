using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

public class MoveSlotRequest
{
    /// <summary>"up" (slot − 1) or "down" (slot + 1).</summary>
    public string Direction { get; set; } = string.Empty;
}

[ApiController]
[Route("api/featured-promo-items")]
public class FeaturedPromoItemsController : ControllerBase
{
    private readonly IFeaturedPromoItemRepository _repository;

    public FeaturedPromoItemsController(IFeaturedPromoItemRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Filtered search: TrainingCenter tab + one-week (Mon–Sun) ScheduleOn window.</summary>
    [HttpPost("query")]
    public async Task<ActionResult<IEnumerable<FeaturedPromoItem>>> Query(
        [FromBody] FeaturedPromoItemQuery query, CancellationToken ct)
    {
        var items = await _repository.QueryAsync(query ?? new FeaturedPromoItemQuery(), ct);
        return Ok(items);
    }

    /// <summary>Single item by pkid.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FeaturedPromoItem>> GetById(int id, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Create an item. The (ScheduleOn, TrainingCenter, Slot) cell must be free.</summary>
    [HttpPost]
    public async Task<ActionResult<FeaturedPromoItem>> Create(
        [FromBody] FeaturedPromoItemRequest request, CancellationToken ct)
    {
        Validate(request);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (await _repository.IsSlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, 0, ct))
        {
            return Conflict($"Slot {request.Slot} on {request.ScheduleOn:yyyy-MM-dd} is already taken.");
        }

        var created = await _repository.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Pkid }, created);
    }

    /// <summary>Update an item (pkid taken from the body).</summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] FeaturedPromoItemRequest request, CancellationToken ct)
    {
        if (request.Pkid == 0)
        {
            ModelState.AddModelError(nameof(request.Pkid), "Pkid is required.");
        }
        Validate(request);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (await _repository.IsSlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, request.Pkid, ct))
        {
            return Conflict($"Slot {request.Slot} on {request.ScheduleOn:yyyy-MM-dd} is already taken.");
        }

        var updated = await _repository.UpdateAsync(request, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Delete an item by pkid.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await _repository.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Move an item one slot up ("-", e.g. 2→1) or down ("+", e.g. 1→2), swapping with any occupant.</summary>
    [HttpPost("{id:int}/move")]
    public async Task<IActionResult> Move(int id, [FromBody] MoveSlotRequest request, CancellationToken ct)
    {
        var delta = request?.Direction?.ToLowerInvariant() switch
        {
            "up" => -1,
            "down" => 1,
            _ => 0
        };
        if (delta == 0)
        {
            ModelState.AddModelError(nameof(MoveSlotRequest.Direction), "Direction must be 'up' or 'down'.");
            return ValidationProblem(ModelState);
        }

        var result = await _repository.MoveSlotAsync(id, delta, ct);
        return result switch
        {
            MoveSlotResult.Moved => NoContent(),
            MoveSlotResult.NotFound => NotFound(),
            _ => Conflict("Slot is already at the boundary (1–3).")
        };
    }

    private void Validate(FeaturedPromoItemRequest request)
    {
        if (request.ScheduleOn == default)
        {
            ModelState.AddModelError(nameof(request.ScheduleOn), "ScheduleOn is required.");
        }
        if (request.TrainingCenterPkid <= 0)
        {
            ModelState.AddModelError(nameof(request.TrainingCenterPkid), "TrainingCenterPkid is required.");
        }
        if (request.Slot is < 1 or > 3)
        {
            ModelState.AddModelError(nameof(request.Slot), "Slot must be 1, 2 or 3.");
        }
        if (request.PromotionPkid <= 0)
        {
            ModelState.AddModelError(nameof(request.PromotionPkid), "PromotionPkid is required (look up the PromoCode first).");
        }
        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            ModelState.AddModelError(nameof(request.Topic), "Topic is required.");
        }
    }
}
