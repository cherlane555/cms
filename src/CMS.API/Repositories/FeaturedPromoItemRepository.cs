using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class FeaturedPromoItemRepository : IFeaturedPromoItemRepository
{
    private readonly IDbConnectionFactory _factory;

    public FeaturedPromoItemRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    private const string BaseSelect = @"
SELECT f.pkid, f.ScheduleOn, f.TrainingCenter_pkid AS TrainingCenterPkid, f.Slot,
       f.Promotion_pkid AS PromotionPkid, f.Topic, f.Description, p.PromoCode
FROM FeaturedPromoItem f
JOIN Promotion2 p ON p.pkid = f.Promotion_pkid";

    public async Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken ct = default)
    {
        var where = new List<string>();
        var p = new DynamicParameters();

        if (query.TrainingCenterPkid.HasValue)
        {
            where.Add("f.TrainingCenter_pkid = @TrainingCenterPkid");
            p.Add("@TrainingCenterPkid", query.TrainingCenterPkid.Value);
        }
        if (query.ScheduleFrom.HasValue)
        {
            where.Add("f.ScheduleOn >= @ScheduleFrom");
            p.Add("@ScheduleFrom", query.ScheduleFrom.Value.Date);
        }
        if (query.ScheduleTo.HasValue)
        {
            where.Add("f.ScheduleOn <= @ScheduleTo");
            p.Add("@ScheduleTo", query.ScheduleTo.Value.Date);
        }

        var whereClause = where.Count > 0 ? $"\nWHERE {string.Join(" AND ", where)}" : string.Empty;
        var sql = $"{BaseSelect}{whereClause}\nORDER BY f.ScheduleOn ASC, f.Slot ASC";

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<FeaturedPromoItem>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<FeaturedPromoItem>(new CommandDefinition(
            $"{BaseSelect}\nWHERE f.pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
    }

    public async Task<bool> IsSlotTakenAsync(DateTime scheduleOn, short trainingCenterPkid, byte slot, int excludePkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var pkid = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(@"
SELECT TOP 1 pkid FROM FeaturedPromoItem
WHERE ScheduleOn = @ScheduleOn AND TrainingCenter_pkid = @TrainingCenterPkid
  AND Slot = @Slot AND pkid <> @ExcludePkid",
            new
            {
                ScheduleOn = scheduleOn.Date,
                TrainingCenterPkid = trainingCenterPkid,
                Slot = slot,
                ExcludePkid = excludePkid
            }, cancellationToken: ct));
        return pkid.HasValue;
    }

    public async Task<FeaturedPromoItem> CreateAsync(FeaturedPromoItemRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);

        var pkid = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO FeaturedPromoItem (ScheduleOn, TrainingCenter_pkid, Slot, Promotion_pkid, Topic, Description)
VALUES (@ScheduleOn, @TrainingCenterPkid, @Slot, @PromotionPkid, @Topic, @Description);
SELECT CAST(SCOPE_IDENTITY() AS int);",
            new
            {
                ScheduleOn = request.ScheduleOn.Date,
                request.TrainingCenterPkid,
                request.Slot,
                request.PromotionPkid,
                request.Topic,
                request.Description
            }, cancellationToken: ct));

        // Re-select to include the joined PromoCode.
        var created = await conn.QuerySingleAsync<FeaturedPromoItem>(new CommandDefinition(
            $"{BaseSelect}\nWHERE f.pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
        return created;
    }

    public async Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE FeaturedPromoItem
SET ScheduleOn = @ScheduleOn,
    TrainingCenter_pkid = @TrainingCenterPkid,
    Slot = @Slot,
    Promotion_pkid = @PromotionPkid,
    Topic = @Topic,
    Description = @Description
WHERE pkid = @Pkid;",
            new
            {
                request.Pkid,
                ScheduleOn = request.ScheduleOn.Date,
                request.TrainingCenterPkid,
                request.Slot,
                request.PromotionPkid,
                request.Topic,
                request.Description
            }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM FeaturedPromoItem WHERE pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
        return affected > 0;
    }

    private sealed class SlotRow
    {
        public DateTime ScheduleOn { get; set; }
        public short TrainingCenterPkid { get; set; }
        public byte Slot { get; set; }
    }

    public async Task<MoveSlotResult> MoveSlotAsync(int pkid, int delta, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var row = await conn.QuerySingleOrDefaultAsync<SlotRow>(new CommandDefinition(@"
SELECT ScheduleOn, TrainingCenter_pkid AS TrainingCenterPkid, Slot
FROM FeaturedPromoItem WHERE pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));
        if (row is null)
        {
            return MoveSlotResult.NotFound;
        }

        var target = row.Slot + delta;
        if (target is < 1 or > 3)
        {
            return MoveSlotResult.OutOfRange;
        }

        var occupantPkid = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(@"
SELECT pkid FROM FeaturedPromoItem
WHERE ScheduleOn = @ScheduleOn AND TrainingCenter_pkid = @TrainingCenterPkid AND Slot = @Slot",
            new { row.ScheduleOn, row.TrainingCenterPkid, Slot = target }, tx, cancellationToken: ct));

        if (occupantPkid.HasValue)
        {
            // Swap via a temporary slot 0 so the (ScheduleOn, TrainingCenter, Slot) unique
            // constraint never sees two rows on the same slot mid-swap.
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE FeaturedPromoItem SET Slot = 0 WHERE pkid = @Pkid",
                new { Pkid = pkid }, tx, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE FeaturedPromoItem SET Slot = @Slot WHERE pkid = @Pkid",
                new { Pkid = occupantPkid.Value, row.Slot }, tx, cancellationToken: ct));
        }

        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE FeaturedPromoItem SET Slot = @Slot WHERE pkid = @Pkid",
            new { Pkid = pkid, Slot = target }, tx, cancellationToken: ct));

        tx.Commit();
        return MoveSlotResult.Moved;
    }
}
