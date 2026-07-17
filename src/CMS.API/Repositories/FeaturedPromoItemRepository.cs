using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class FeaturedPromoItemRepository : IFeaturedPromoItemRepository
{
    private const string TableName = "FeaturedPromoItem";

    private readonly IDbConnectionFactory _factory;
    private readonly IRowAuditWriter _audit;

    public FeaturedPromoItemRepository(IDbConnectionFactory factory, IRowAuditWriter audit)
    {
        _factory = factory;
        _audit = audit;
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

    /// <summary>
    /// Authoritative slot-uniqueness check, run on the caller's own connection/transaction with
    /// (UPDLOCK, HOLDLOCK) so a concurrent Create/Update targeting the same
    /// (ScheduleOn, TrainingCenter, Slot) cell blocks until this transaction commits or rolls
    /// back, instead of both transactions reading "free" on separate connections and racing to
    /// insert. <see cref="IsSlotTakenAsync"/> alone can't close that window — it runs on its own
    /// connection before the write transaction even opens.
    /// </summary>
    private static async Task EnsureSlotFreeAsync(
        System.Data.IDbConnection conn, System.Data.IDbTransaction tx,
        DateTime scheduleOn, short trainingCenterPkid, byte slot, int excludePkid, CancellationToken ct)
    {
        var taken = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(@"
SELECT TOP 1 pkid FROM FeaturedPromoItem WITH (UPDLOCK, HOLDLOCK)
WHERE ScheduleOn = @ScheduleOn AND TrainingCenter_pkid = @TrainingCenterPkid
  AND Slot = @Slot AND pkid <> @ExcludePkid",
            new { ScheduleOn = scheduleOn, TrainingCenterPkid = trainingCenterPkid, Slot = slot, ExcludePkid = excludePkid },
            tx, cancellationToken: ct));

        if (taken.HasValue)
        {
            throw new SlotConflictException($"Slot {slot} on {scheduleOn:yyyy-MM-dd} is already taken.");
        }
    }

    public async Task<FeaturedPromoItem> CreateAsync(FeaturedPromoItemRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await EnsureSlotFreeAsync(conn, tx, request.ScheduleOn.Date, request.TrainingCenterPkid, request.Slot, excludePkid: 0, ct);

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
            }, tx, cancellationToken: ct));

        // Re-select to include the joined PromoCode.
        var created = await conn.QuerySingleAsync<FeaturedPromoItem>(new CommandDefinition(
            $"{BaseSelect}\nWHERE f.pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));

        await _audit.LogInsertAsync(TableName, created, conn, tx, ct);
        tx.Commit();

        return created;
    }

    public async Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // Load the "before" row first so the audit can list exactly the changed columns.
        var before = await conn.QuerySingleOrDefaultAsync<FeaturedPromoItem>(new CommandDefinition(
            $"{BaseSelect}\nWHERE f.pkid = @Pkid",
            new { request.Pkid }, tx, cancellationToken: ct));
        if (before is null)
        {
            return false;
        }

        await EnsureSlotFreeAsync(conn, tx, request.ScheduleOn.Date, request.TrainingCenterPkid, request.Slot, excludePkid: request.Pkid, ct);

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
            }, tx, cancellationToken: ct));

        if (affected == 0)
        {
            return false;
        }

        // The "after" image: the before row with the updatable columns applied. PromoCode is a
        // joined label copied from before so it never shows up as a changed column.
        var after = new FeaturedPromoItem
        {
            Pkid = before.Pkid,
            ScheduleOn = request.ScheduleOn.Date,
            TrainingCenterPkid = request.TrainingCenterPkid,
            Slot = request.Slot,
            PromotionPkid = request.PromotionPkid,
            Topic = request.Topic,
            Description = request.Description,
            PromoCode = before.PromoCode
        };

        await _audit.LogUpdateAsync(TableName, before, after, conn, tx, ct);
        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // Load the row first so its first string column is still available for the audit.
        var row = await conn.QuerySingleOrDefaultAsync<FeaturedPromoItem>(new CommandDefinition(
            $"{BaseSelect}\nWHERE f.pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));
        if (row is null)
        {
            return false;
        }

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM FeaturedPromoItem WHERE pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));
        if (affected == 0)
        {
            return false;
        }

        await _audit.LogDeleteAsync(TableName, row, conn, tx, ct);
        tx.Commit();
        return true;
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

        // UPDLOCK+HOLDLOCK: two concurrent moves in the same (ScheduleOn, TrainingCenter) group
        // must serialize on these reads rather than both proceeding with a stale occupant view —
        // without it, concurrent moves can deadlock (locks taken in different orders) or lose an
        // update (second move overwrites the first's swap using pre-swap data).
        var row = await conn.QuerySingleOrDefaultAsync<SlotRow>(new CommandDefinition(@"
SELECT ScheduleOn, TrainingCenter_pkid AS TrainingCenterPkid, Slot
FROM FeaturedPromoItem WITH (UPDLOCK, HOLDLOCK) WHERE pkid = @Pkid",
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
SELECT pkid FROM FeaturedPromoItem WITH (UPDLOCK, HOLDLOCK)
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

            await LogSlotChangeAsync(conn, tx, occupantPkid.Value, (byte)target, ct);
        }

        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE FeaturedPromoItem SET Slot = @Slot WHERE pkid = @Pkid",
            new { Pkid = pkid, Slot = target }, tx, cancellationToken: ct));

        await LogSlotChangeAsync(conn, tx, pkid, row.Slot, ct);

        tx.Commit();
        return MoveSlotResult.Moved;
    }

    /// <summary>Audits a slot move as an Update whose before/after differ only in Slot.</summary>
    private async Task LogSlotChangeAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        int pkid,
        byte oldSlot,
        CancellationToken ct)
    {
        // The row was already updated: re-select it (new Slot) and reconstruct the before image.
        var after = await conn.QuerySingleAsync<FeaturedPromoItem>(new CommandDefinition(
            $"{BaseSelect}\nWHERE f.pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));

        var before = new FeaturedPromoItem
        {
            Pkid = after.Pkid,
            ScheduleOn = after.ScheduleOn,
            TrainingCenterPkid = after.TrainingCenterPkid,
            Slot = oldSlot,
            PromotionPkid = after.PromotionPkid,
            Topic = after.Topic,
            Description = after.Description,
            PromoCode = after.PromoCode
        };

        await _audit.LogUpdateAsync(TableName, before, after, conn, tx, ct);
    }
}
