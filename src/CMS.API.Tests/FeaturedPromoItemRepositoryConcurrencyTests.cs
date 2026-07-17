using System.Security.Claims;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Repositories;
using Dapper;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests;

/// <summary>
/// Integration tests against the local CMS database proving the (ScheduleOn, TrainingCenter,
/// Slot) uniqueness check is race-safe: two concurrent CreateAsync calls for the same cell must
/// result in exactly one success and one SlotConflictException — never two successes, and never
/// a raw SqlException leaking out as a 500. Before the fix, both calls read the pre-check as
/// "free" on separate connections and raced to insert.
/// </summary>
public class FeaturedPromoItemRepositoryConcurrencyTests
{
    private const string ConnString =
        "Server=.\\SQLEXPRESS;Database=CMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    // Existing lookup rows in the local dev DB — read-only reference, never mutated by this test.
    private const short TrainingCenterPkid = 3;
    private const int PromotionPkid = 3419;

    private readonly SqlConnectionFactory _factory = new(ConnString);

    private FeaturedPromoItemRepository CreateRepo()
    {
        var identity = new ClaimsIdentity(new[] { new Claim("userName", "concurrency-tester") }, authenticationType: "Test");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return new FeaturedPromoItemRepository(_factory, new RowAuditWriter(_factory, accessor));
    }

    private static FeaturedPromoItemRequest NewRequest(DateTime scheduleOn, byte slot) => new()
    {
        ScheduleOn = scheduleOn,
        TrainingCenterPkid = TrainingCenterPkid,
        Slot = slot,
        PromotionPkid = PromotionPkid,
        Topic = $"concurrency-test-{Guid.NewGuid():N}",
        Description = "created by FeaturedPromoItemRepositoryConcurrencyTests"
    };

    private async Task CleanupAsync(DateTime scheduleOn, byte slot)
    {
        using var conn = await _factory.CreateOpenConnectionAsync();
        var pkids = (await conn.QueryAsync<int>(
            "SELECT pkid FROM FeaturedPromoItem WHERE ScheduleOn = @ScheduleOn AND TrainingCenter_pkid = @TrainingCenterPkid AND Slot = @Slot",
            new { ScheduleOn = scheduleOn, TrainingCenterPkid, Slot = slot })).ToList();

        foreach (var pkid in pkids)
        {
            await conn.ExecuteAsync(
                "DELETE FROM RowAudit WHERE TableName = 'FeaturedPromoItem' AND PrimaryKeyValues = @Pk",
                new { Pk = pkid.ToString() });
        }
        await conn.ExecuteAsync(
            "DELETE FROM FeaturedPromoItem WHERE ScheduleOn = @ScheduleOn AND TrainingCenter_pkid = @TrainingCenterPkid AND Slot = @Slot",
            new { ScheduleOn = scheduleOn, TrainingCenterPkid, Slot = slot });
    }

    [Fact]
    public async Task ConcurrentCreate_SameSlot_ExactlyOneSucceeds()
    {
        // Far-future, randomized date so this test can never collide with real app data or a
        // parallel test run.
        var scheduleOn = DateTime.Today.AddYears(5).AddDays(Random.Shared.Next(0, 3000));
        const byte slot = 2;

        try
        {
            var repoA = CreateRepo();
            var repoB = CreateRepo();

            var taskA = repoA.CreateAsync(NewRequest(scheduleOn, slot), CancellationToken.None);
            var taskB = repoB.CreateAsync(NewRequest(scheduleOn, slot), CancellationToken.None);

            var results = await Task.WhenAll(
                taskA.ContinueWith(t => (ok: !t.IsFaulted, ex: t.IsFaulted ? t.Exception!.InnerException : null)),
                taskB.ContinueWith(t => (ok: !t.IsFaulted, ex: t.IsFaulted ? t.Exception!.InnerException : null)));

            Assert.Single(results, r => r.ok);
            var failure = Assert.Single(results, r => !r.ok);
            Assert.IsType<SlotConflictException>(failure.ex);

            using var conn = await _factory.CreateOpenConnectionAsync();
            var rowCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM FeaturedPromoItem WHERE ScheduleOn = @ScheduleOn AND TrainingCenter_pkid = @TrainingCenterPkid AND Slot = @Slot",
                new { ScheduleOn = scheduleOn, TrainingCenterPkid, Slot = slot });
            Assert.Equal(1, rowCount);
        }
        finally
        {
            await CleanupAsync(scheduleOn, slot);
        }
    }
}
