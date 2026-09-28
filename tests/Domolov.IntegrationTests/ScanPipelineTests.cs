using Domolov.Application.Homes;
using Domolov.Application.Notifications;
using Domolov.Application.Scans;
using Domolov.Application.Watches;
using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Domolov.Domain.Notifications;
using Domolov.Domain.Scans;
using Domolov.Infrastructure.Scanning;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Domolov.IntegrationTests;

/// <summary>
/// Drives real ScanRuns through the fixture provider, whose data evolves per round:
/// round 2 adds a Duplicate (6412109 of 6411009), a Possible match (6412111 of 6411011), two new
/// Homes, a price drop (6411003) and removes 6411005; round 3 delists it; round 4 reposts it.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ScanPipelineTests(PostgresFixture postgres)
{
    private const string SearchUrl = "https://fixtures.domolov.test/oglasi-prodaja/ljubljana/";

    [Fact]
    public async Task Four_rounds_cover_baseline_changes_duplicates_delisting_and_reposts()
    {
        await using var app = await postgres.CreateAppAsync();
        var watch = await app.RunAsync<CreateWatchHandler, WatchResponse>(h =>
            h.HandleAsync(
                new CreateWatchRequest(
                    "Ljubljana",
                    SearchUrl,
                    InitialRoute: new CreateNotificationRouteRequest(
                        NotificationChannel.Discord,
                        "https://discord.com/api/webhooks/1/test",
                        [
                            NotificationTrigger.NewListing,
                            NotificationTrigger.PriceDecreased,
                            NotificationTrigger.Reposted,
                        ]
                    )
                ),
                CancellationToken.None
            )
        );

        // Round 1: baseline stores everything silently.
        var round1 = await RunScanAsync(app, watch.Id);
        round1.Status.Should().Be(ScanRunStatus.Baseline);
        app.Notifier.Sent.Should().BeEmpty();
        (await app.QueryAsync(db => db.Homes.CountAsync()))
            .Should()
            .Be(FixtureListingProvider.CardsForRound(1).Count);

        // Round 2: new Homes and a price drop notify; the Duplicate auto-links silently; the
        // Possible match waits for review.
        app.Clock.Advance(TimeSpan.FromHours(6));
        var round2 = await RunScanAsync(app, watch.Id);
        round2.Status.Should().Be(ScanRunStatus.Succeeded);
        round2.CrawlReachedEnd.Should().BeTrue();
        round2
            .NewCount.Should()
            .Be(3, "6411017, 6411018 and the unmatched possible 6412111 are new Homes");
        app.Notifier.Sent.Select(m => m.Kind).Should().Contain(ListingChangeKind.PriceDecreased);
        app.Notifier.Sent.Count(m => m.Kind == ListingChangeKind.New).Should().Be(3);

        var duplicateHome = await HomeOfAsync(app, "6412109");
        (await HomeOfAsync(app, "6411009")).Should().Be(duplicateHome);
        var possible = await app.RunAsync<ListMatchesHandler, IReadOnlyList<HomeMatchResponse>>(h =>
            h.HandleAsync(HomeMatchState.Possible, CancellationToken.None)
        );
        possible.Should().ContainSingle(m => m.Listing.Id == ListingIdAsync(app, "6412111").Result);

        // Round 3: second Complete ScanRun without 6411005 delists it; its Home goes off market.
        app.Clock.Advance(TimeSpan.FromHours(6));
        await RunScanAsync(app, watch.Id);
        var gone = await app.QueryAsync(db =>
            db.Listings.SingleAsync(l => l.ExternalId.EndsWith("-6411005"))
        );
        gone.DelistedAt.Should().NotBeNull();
        (await app.QueryAsync(db => db.Homes.SingleAsync(h => h.Id == gone.HomeId)))
            .OffMarketAt.Should()
            .NotBeNull();

        // Round 4: the same flat comes back under a new id; it joins the old Home as a Repost.
        app.Notifier.Clear();
        app.Clock.Advance(TimeSpan.FromDays(10));
        var round4 = await RunScanAsync(app, watch.Id);
        round4.RepostCount.Should().Be(1);
        (await HomeOfAsync(app, "6412105")).Should().Be(gone.HomeId);
        var home = await app.RunAsync<GetHomeHandler, HomeDetailResponse>(h =>
            h.HandleAsync(gone.HomeId, CancellationToken.None)
        );
        home.Home.OffMarketAt.Should().BeNull();
        home.Home.RepostCount.Should().Be(1);
        home.Listings.Select(l => l.Role)
            .Should()
            .Equal(HomeListingRole.Original, HomeListingRole.Repost);
        var repost = app
            .Notifier.Sent.Should()
            .ContainSingle(m => m.Kind == ListingChangeKind.Reposted)
            .Subject;
        repost.Body.Should().Contain("cheaper").And.Contain("285.000 €");
    }

    [Fact]
    public async Task Dismissed_homes_stay_silent_and_run_now_on_a_paused_watch_still_works()
    {
        await using var app = await postgres.CreateAppAsync();
        var watch = await app.RunAsync<CreateWatchHandler, WatchResponse>(h =>
            h.HandleAsync(
                new CreateWatchRequest(
                    "Paused",
                    SearchUrl + "paused/",
                    IsPaused: true,
                    InitialRoute: new CreateNotificationRouteRequest(
                        NotificationChannel.Discord,
                        "https://discord.com/api/webhooks/2/x"
                    )
                ),
                CancellationToken.None
            )
        );
        watch.IsPaused.Should().BeTrue();
        watch.NextRunAt.Should().BeNull();

        await RunScanAsync(app, watch.Id, manual: true);
        var droppingHome = await HomeOfAsync(app, "6411003");
        await app.RunAsync<DismissHomeHandler>(h =>
            h.HandleAsync(droppingHome, CancellationToken.None)
        );

        app.Clock.Advance(TimeSpan.FromHours(1));
        await RunScanAsync(app, watch.Id, manual: true);

        app.Notifier.Sent.Should().NotContain(m => m.HomeId == droppingHome);
        app.Notifier.Sent.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Interrupted_runs_are_recovered_so_the_watch_can_scan_again()
    {
        await using var app = await postgres.CreateAppAsync();
        var watch = await app.RunAsync<CreateWatchHandler, WatchResponse>(h =>
            h.HandleAsync(
                new CreateWatchRequest("Crash", SearchUrl + "crash/"),
                CancellationToken.None
            )
        );
        await app.QueryAsync(async db =>
        {
            var run = await db.ScanRuns.SingleAsync(r => r.WatchId == watch.Id);
            run.Start(app.Clock.GetUtcNow());
            return await db.SaveChangesAsync();
        });

        var recovered = await app.RunAsync<ScanQueue, int>(q =>
            q.RecoverInterruptedAsync(CancellationToken.None)
        );
        var next = await app.RunAsync<ScanQueue, Guid?>(q =>
            q.EnqueueAsync(watch.Id, isManual: true, CancellationToken.None)
        );

        recovered.Should().Be(1);
        (
            await app.QueryAsync(db =>
                db.ScanRuns.CountAsync(r => r.Status == ScanRunStatus.Interrupted)
            )
        )
            .Should()
            .Be(1);
        next.Should().NotBeNull();
    }

    [Fact]
    public async Task Shutdown_sweep_interrupts_every_running_scan_and_leaves_queued_ones()
    {
        await using var app = await postgres.CreateAppAsync();
        var first = await app.RunAsync<CreateWatchHandler, WatchResponse>(h =>
            h.HandleAsync(
                new CreateWatchRequest("Sweep A", SearchUrl + "sweep-a/"),
                CancellationToken.None
            )
        );
        var second = await app.RunAsync<CreateWatchHandler, WatchResponse>(h =>
            h.HandleAsync(
                new CreateWatchRequest("Sweep B", SearchUrl + "sweep-b/"),
                CancellationToken.None
            )
        );
        await app.QueryAsync(async db =>
        {
            var running = await db
                .ScanRuns.Where(r => r.WatchId == first.Id || r.WatchId == second.Id)
                .ToListAsync();
            foreach (var run in running)
            {
                run.Start(app.Clock.GetUtcNow());
            }

            db.ScanRuns.Add(new ScanRun(first.Id, isManual: true, app.Clock.GetUtcNow()));
            return await db.SaveChangesAsync();
        });

        var recovered = await app.RunAsync<ScanQueue, int>(q =>
            q.RecoverInterruptedAsync(CancellationToken.None)
        );

        recovered.Should().Be(2);
        var left = await app.QueryAsync(db =>
            db.ScanRuns.Where(r => r.WatchId == first.Id || r.WatchId == second.Id)
                .Select(r => r.Status)
                .ToListAsync()
        );
        left.Count(s => s == ScanRunStatus.Interrupted).Should().Be(2);
        left.Should().ContainSingle(s => s == ScanRunStatus.Queued);
    }

    [Fact]
    public async Task Unlinking_splits_the_ad_into_its_own_home_and_remembers_the_pair()
    {
        await using var app = await postgres.CreateAppAsync();
        var watch = await app.RunAsync<CreateWatchHandler, WatchResponse>(h =>
            h.HandleAsync(
                new CreateWatchRequest("Split", SearchUrl + "split/"),
                CancellationToken.None
            )
        );
        await RunScanAsync(app, watch.Id);
        app.Clock.Advance(TimeSpan.FromHours(6));
        await RunScanAsync(app, watch.Id);
        var shared = await HomeOfAsync(app, "6411009");
        var duplicateId = await ListingIdAsync(app, "6412109");

        var split = await app.RunAsync<UnlinkListingHandler, HomeSummaryResponse>(h =>
            h.HandleAsync(shared, duplicateId, CancellationToken.None)
        );

        split.Id.Should().NotBe(shared);
        (await HomeOfAsync(app, "6412109")).Should().Be(split.Id);
        (
            await app.QueryAsync(db =>
                db.HomeMatches.AnyAsync(m =>
                    m.ListingId == duplicateId
                    && m.HomeId == shared
                    && m.State == HomeMatchState.Rejected
                )
            )
        )
            .Should()
            .BeTrue();
    }

    private static async Task<ScanRun> RunScanAsync(TestApp app, Guid watchId, bool manual = false)
    {
        var runId = await app.RunAsync<ScanQueue, Guid?>(q =>
            q.EnqueueAsync(watchId, manual, CancellationToken.None)
        );
        runId.Should().NotBeNull();
        await app.RunAsync<ScanExecutor, bool>(e =>
            e.RunAsync(runId!.Value, CancellationToken.None)
        );
        return await app.QueryAsync(db =>
            db.ScanRuns.AsNoTracking().SingleAsync(r => r.Id == runId)
        );
    }

    private static Task<Guid> HomeOfAsync(TestApp app, string catalogueId) =>
        app.QueryAsync(db =>
            db.Listings.Where(l => l.ExternalId.EndsWith("-" + catalogueId))
                .Select(l => l.HomeId)
                .SingleAsync()
        );

    private static Task<Guid> ListingIdAsync(TestApp app, string catalogueId) =>
        app.QueryAsync(db =>
            db.Listings.Where(l => l.ExternalId.EndsWith("-" + catalogueId))
                .Select(l => l.Id)
                .SingleAsync()
        );
}
