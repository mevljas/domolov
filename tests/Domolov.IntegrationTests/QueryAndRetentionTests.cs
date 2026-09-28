using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Application.Dashboard;
using Domolov.Application.Homes;
using Domolov.Application.Retention;
using Domolov.Application.Scans;
using Domolov.Application.Watches;
using Domolov.Domain.Homes;
using Domolov.Domain.Scans;
using Domolov.Infrastructure.Signals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Domolov.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class QueryAndRetentionTests(PostgresFixture postgres)
{
    private static async Task<(TestApp App, Guid WatchId)> SeededAsync(
        PostgresFixture postgres,
        Dictionary<string, string?>? settings = null
    )
    {
        var app = await postgres.CreateAppAsync(settings: settings);
        var watch = await app.RunAsync<CreateWatchHandler, WatchResponse>(h =>
            h.HandleAsync(
                new CreateWatchRequest(
                    "Seed",
                    $"https://fixtures.domolov.test/{Guid.NewGuid():N}/"
                ),
                CancellationToken.None
            )
        );
        foreach (var _ in Enumerable.Range(0, 3))
        {
            var runId = await app.RunAsync<ScanQueue, Guid?>(q =>
                q.EnqueueAsync(watch.Id, true, CancellationToken.None)
            );
            await app.RunAsync<ScanExecutor, bool>(e =>
                e.RunAsync(runId!.Value, CancellationToken.None)
            );
            app.Clock.Advance(TimeSpan.FromHours(6));
        }

        return (app, watch.Id);
    }

    [Fact]
    public async Task Homes_feed_filters_sorts_and_pages_in_sql()
    {
        var (app, watchId) = await SeededAsync(postgres);
        await using var _ = app;

        async Task<PagedResponse<HomeSummaryResponse>> Search(HomeSearchQuery q) =>
            await app.RunAsync<SearchHomesHandler, PagedResponse<HomeSummaryResponse>>(h =>
                h.HandleAsync(q, CancellationToken.None)
            );

        var all = await Search(new HomeSearchQuery(PageSize: 100));
        all.Items.Should().OnlyContain(h => h.OffMarketAt == null);

        var byPrice = await Search(new HomeSearchQuery(Sort: HomeSort.PriceAsc, PageSize: 100));
        byPrice.Items.Select(h => h.Price).Should().BeInAscendingOrder();

        var houses = await Search(new HomeSearchQuery(PropertyType: "hiša", PageSize: 100));
        houses.Items.Should().NotBeEmpty().And.OnlyContain(h => h.PropertyType == "Hiša");

        var big = await Search(new HomeSearchQuery(MinSize: 100, MaxPrice: 700_000));
        big.Items.Should().OnlyContain(h => h.SizeM2 >= 100 && h.Price <= 700_000);

        var drops = await Search(new HomeSearchQuery(Sort: HomeSort.PriceDrop));
        drops.Items.Should().OnlyContain(h => h.Price < h.PreviousPrice);

        var page = await Search(new HomeSearchQuery(Page: 2, PageSize: 5));
        page.Items.Should().HaveCount(5);
        page.Total.Should().Be(all.Total);

        var text = await Search(new HomeSearchQuery(Q: "trnovo"));
        text.Items.Should().ContainSingle().Which.Location.Should().Contain("TRNOVO");

        var byWatch = await Search(new HomeSearchQuery(WatchId: watchId, PageSize: 100));
        byWatch.Total.Should().Be(all.Total);

        var offMarket = await Search(new HomeSearchQuery(Status: MarketStatus.OffMarket));
        offMarket.Items.Should().ContainSingle().Which.Title.Should().Contain("MOSTE");
    }

    [Fact]
    public async Task Bookmark_board_dismissal_and_mark_all_seen()
    {
        var (app, _) = await SeededAsync(postgres);
        await using var _ = app;
        var first = (
            await app.RunAsync<SearchHomesHandler, PagedResponse<HomeSummaryResponse>>(h =>
                h.HandleAsync(new HomeSearchQuery(PageSize: 2), CancellationToken.None)
            )
        ).Items;

        await app.RunAsync<SetBookmarkHandler, BookmarkResponse>(h =>
            h.HandleAsync(
                first[0].Id,
                new SetBookmarkRequest(BookmarkStage.Viewed, "Sunny"),
                CancellationToken.None
            )
        );
        await app.RunAsync<DismissHomeHandler>(h =>
            h.HandleAsync(first[1].Id, CancellationToken.None)
        );
        var seen = await app.RunAsync<MarkAllHomesSeenHandler, CountResponse>(h =>
            h.HandleAsync(new MarkAllSeenRequest(), CancellationToken.None)
        );

        var board = await app.RunAsync<GetBookmarkBoardHandler, BookmarkBoardResponse>(h =>
            h.HandleAsync(CancellationToken.None)
        );
        board
            .Columns.Single(c => c.Stage == BookmarkStage.Viewed)
            .Homes.Should()
            .ContainSingle(h => h.Id == first[0].Id);
        var dismissed = await app.RunAsync<SearchHomesHandler, PagedResponse<HomeSummaryResponse>>(
            h =>
                h.HandleAsync(
                    new HomeSearchQuery(Dismissed: DismissedFilter.Only),
                    CancellationToken.None
                )
        );
        dismissed.Items.Should().ContainSingle(h => h.Id == first[1].Id);
        seen.Count.Should().BeGreaterThan(0);
        var dashboard = await app.RunAsync<GetDashboardHandler, DashboardResponse>(h =>
            h.HandleAsync(CancellationToken.None)
        );
        dashboard.Stats.UnseenHomes.Should().Be(0);
        dashboard.Stats.BookmarkedHomes.Should().Be(1);
        dashboard.Stats.PossibleMatches.Should().Be(1);
    }

    [Fact]
    public async Task Retention_purges_old_delisted_listings_but_keeps_bookmarked_homes()
    {
        var (app, _) = await SeededAsync(
            postgres,
            new() { ["Domolov:Retention:DelistedDays"] = "7" }
        );
        await using var _ = app;
        var delisted = await app.QueryAsync(db =>
            db.Listings.SingleAsync(l => l.DelistedAt != null)
        );
        var keep = await app.QueryAsync(db =>
            db.Listings.Where(l => l.ExternalId.EndsWith("-6411001"))
                .Select(l => l.HomeId)
                .SingleAsync()
        );
        await app.RunAsync<SetBookmarkHandler, BookmarkResponse>(h =>
            h.HandleAsync(
                keep,
                new SetBookmarkRequest(BookmarkStage.Interested, null),
                CancellationToken.None
            )
        );
        await app.QueryAsync(db =>
            db.Listings.Where(l => l.HomeId == keep)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(l => l.DelistedAt, app.Clock.GetUtcNow().AddDays(-1))
                )
        );

        app.Clock.Advance(TimeSpan.FromDays(8));
        var run = await app.RunAsync<RetentionCleaner, Domain.Retention.CleanupRun>(c =>
            c.RunAsync(true, 1234, CancellationToken.None)
        );

        run.Error.Should().BeNull();
        run.DeletedListings.Should().Be(1);
        run.DeletedHomes.Should().Be(1);
        run.BrowserProfileBytes.Should().Be(1234);
        (await app.QueryAsync(db => db.Listings.AnyAsync(l => l.Id == delisted.Id)))
            .Should()
            .BeFalse();
        (await app.QueryAsync(db => db.Homes.AnyAsync(h => h.Id == keep))).Should().BeTrue();
        (await app.QueryAsync(db => db.PriceObservations.AnyAsync(p => p.ListingId == delisted.Id)))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task Retention_keeps_the_latest_scan_runs_per_watch()
    {
        var (app, _) = await SeededAsync(
            postgres,
            new()
            {
                ["Domolov:Retention:ScanRunDays"] = "1",
                ["Domolov:Retention:ScanRunKeepPerWatch"] = "2",
            }
        );
        await using var _ = app;
        app.Clock.Advance(TimeSpan.FromDays(5));

        var run = await app.RunAsync<RetentionCleaner, Domain.Retention.CleanupRun>(c =>
            c.RunAsync(false, null, CancellationToken.None)
        );

        run.DeletedScanRuns.Should().Be(1);
        (await app.QueryAsync(db => db.ScanRuns.CountAsync())).Should().Be(2);
    }

    [Fact]
    public async Task Storage_stats_report_the_database_size()
    {
        await using var app = await postgres.CreateAppAsync();
        var storage = await app.RunAsync<GetStorageHandler, StorageResponse>(h =>
            h.HandleAsync(CancellationToken.None)
        );
        storage.DatabaseBytes.Should().BeGreaterThan(0);
        storage.Tables.Should().Contain(t => t.Name == "Listings");
        storage.NextCleanupAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Signals_round_trip_through_listen_notify()
    {
        await using var app = await postgres.CreateAppAsync();
        var bus = app.Services.GetRequiredService<SignalBus>();
        var listener = new PostgresSignalListener(
            app.Services.GetRequiredService<Npgsql.NpgsqlDataSource>(),
            bus,
            NullLogger<PostgresSignalListener>.Instance
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var received = Task.Run(async () =>
        {
            await foreach (var signal in bus.SubscribeAsync(cts.Token))
            {
                if (signal.Channel == SignalChannels.ScanRequested)
                {
                    return signal.Payload;
                }
            }

            return null;
        });
        await listener.StartAsync(cts.Token);
        var publisher = app.Services.GetRequiredService<ISignalPublisher>();

        string? payload = null;
        for (var i = 0; i < 20 && payload is null; i++)
        {
            await publisher.PublishAsync(SignalChannels.ScanRequested, "hello", cts.Token);
            payload =
                await Task.WhenAny(received, Task.Delay(500, cts.Token)) == received
                    ? await received
                    : null;
        }

        await listener.StopAsync(CancellationToken.None);
        payload.Should().Be("hello");
    }
}
