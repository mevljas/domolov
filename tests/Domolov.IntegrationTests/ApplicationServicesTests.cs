using Domolov.Application.Abstractions;
using Domolov.Application.Contracts;
using Domolov.Application.Services;
using Domolov.Domain.Entities;
using Domolov.Domain.Enums;
using Domolov.Domain.Providers;
using Domolov.Domain.Services;
using Domolov.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Domolov.IntegrationTests;

public sealed class ApplicationServicesTests
{
    [Fact]
    public async Task WatchService_crud_and_routes_cover_happy_and_missing_paths()
    {
        await using var root = BuildHost();
        await using var scope = root.CreateAsyncScope();
        var watches = scope.ServiceProvider.GetRequiredService<IWatchService>();

        var created = await watches.CreateAsync(
            new CreateWatchRequest
            {
                Name = "  Alpha  ",
                SearchUrl = "https://www.nepremicnine.net/oglasi-prodaja/",
                Cron = "  ",
                IsEnabled = true,
            }
        );
        created.Name.Should().Be("Alpha");
        created.Cron.Should().Be("0 */6 * * *");
        created.ProviderId.Should().Be(NepremicnineParsing.ProviderId);

        var emptyCron = await watches.CreateAsync(
            new CreateWatchRequest
            {
                Name = "Beta",
                SearchUrl = "https://www.nepremicnine.net/oglasi-prodaja/ljubljana/",
                Cron = "15 * * * *",
            }
        );

        var all = await watches.GetAllAsync();
        all.Should().HaveCount(2);
        all.Select(w => w.Name).Should().ContainInOrder("Alpha", "Beta");

        (await watches.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
        var fetched = await watches.GetByIdAsync(created.Id);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);

        var updated = await watches.UpdateAsync(
            created.Id,
            new UpdateWatchRequest
            {
                Name = "Alpha2",
                SearchUrl = "https://www.nepremicnine.net/oglasi-oddaja/",
                Cron = "30 * * * *",
                IsEnabled = false,
            }
        );
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Alpha2");
        updated.IsEnabled.Should().BeFalse();
        updated.Cron.Should().Be("30 * * * *");

        var act = () =>
            watches.CreateAsync(
                new CreateWatchRequest
                {
                    Name = "Bad",
                    SearchUrl = "https://www.nepremicnine.net/oglasi-prodaja/",
                    Cron = "not-a-cron",
                }
            );
        await act.Should().ThrowAsync<ArgumentException>();

        var weekday = await watches.CreateAsync(
            new CreateWatchRequest
            {
                Name = "Weekday",
                SearchUrl = "https://www.nepremicnine.net/oglasi-prodaja/maribor/",
                Cron = "0 9 * * 1-5",
            }
        );
        weekday.Cron.Should().Be("0 9 * * 1-5");

        (await watches.UpdateAsync(Guid.NewGuid(), updated.ToUpdate())).Should().BeNull();

        var route = await watches.AddRouteAsync(
            created.Id,
            new CreateNotificationRouteRequest
            {
                Channel = NotificationChannel.Discord,
                Destination = "https://discord.test/hook",
                Triggers = NotificationTrigger.NewListing,
            }
        );
        route.Should().NotBeNull();
        (
            await watches.AddRouteAsync(
                Guid.NewGuid(),
                new CreateNotificationRouteRequest
                {
                    Channel = NotificationChannel.Email,
                    Destination = "a@b.c",
                }
            )
        )
            .Should()
            .BeNull();

        (await watches.DeleteRouteAsync(created.Id, Guid.NewGuid())).Should().BeFalse();
        (await watches.DeleteRouteAsync(created.Id, route!.Id)).Should().BeTrue();
        (await watches.DeleteRouteAsync(created.Id, route.Id)).Should().BeFalse();

        (await watches.DeleteAsync(Guid.NewGuid())).Should().BeFalse();
        (await watches.DeleteAsync(emptyCron.Id)).Should().BeTrue();
        (await watches.GetAllAsync()).Should().ContainSingle(w => w.Id == created.Id);
    }

    [Fact]
    public async Task ListingQueryService_filters_bookmarks_and_details()
    {
        await using var root = BuildHost();
        await using var scope = root.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var listings = scope.ServiceProvider.GetRequiredService<IListingQueryService>();

        var watch = new Watch
        {
            Name = "w",
            ProviderId = "nepremicnine",
            SearchUrl = "https://www.nepremicnine.net/x/",
        };
        var otherWatch = new Watch
        {
            Name = "w2",
            ProviderId = "nepremicnine",
            SearchUrl = "https://www.nepremicnine.net/y/",
        };
        db.Watches.AddRange(watch, otherWatch);

        var listing = new Listing
        {
            ProviderId = "nepremicnine",
            ExternalId = "111",
            Url = "https://www.nepremicnine.net/oglasi/111/",
            Title = "Flat",
            Description = "Nice",
            PropertyType = "Stanovanje",
            Rooms = "2",
            SizeText = "50 m2",
            YearText = "2000",
            FloorText = "3",
            ImageUrl = "https://img.example/a.jpg",
        };
        var orphan = new Listing
        {
            ProviderId = "nepremicnine",
            ExternalId = "222",
            Url = "https://www.nepremicnine.net/oglasi/222/",
            Title = "House",
        };
        db.Listings.AddRange(listing, orphan);
        await db.SaveChangesAsync();

        db.PriceObservations.Add(
            new PriceObservation
            {
                ListingId = listing.Id,
                Amount = 100_000,
                Currency = "EUR",
                ObservedAt = DateTimeOffset.UtcNow.AddDays(-2),
            }
        );
        db.PriceObservations.Add(
            new PriceObservation
            {
                ListingId = listing.Id,
                Amount = 95_000,
                Currency = "EUR",
                ObservedAt = DateTimeOffset.UtcNow.AddDays(-1),
            }
        );
        db.WatchSightings.Add(new WatchSighting { WatchId = watch.Id, ListingId = listing.Id });
        await db.SaveChangesAsync();

        var forWatch = await listings.GetForWatchAsync(watch.Id, false);
        forWatch.Should().ContainSingle(l => l.ExternalId == "111");
        forWatch[0].LatestPrice.Should().Be(95_000);
        forWatch[0].IsBookmarked.Should().BeFalse();

        (await listings.GetForWatchAsync(null, true)).Should().BeEmpty();
        (await listings.SetBookmarkAsync(Guid.NewGuid(), true)).Should().BeFalse();
        (await listings.SetBookmarkAsync(listing.Id, true)).Should().BeTrue();
        (await listings.SetBookmarkAsync(listing.Id, true)).Should().BeTrue();

        var bookmarked = await listings.GetForWatchAsync(null, true);
        bookmarked.Should().ContainSingle(l => l.Id == listing.Id && l.IsBookmarked);

        var detail = await listings.GetDetailAsync(listing.Id);
        detail.Should().NotBeNull();
        detail!.Prices.Should().HaveCount(2);
        detail.Prices[0].Amount.Should().Be(100_000);
        detail.IsBookmarked.Should().BeTrue();
        (await listings.GetDetailAsync(Guid.NewGuid())).Should().BeNull();

        (await listings.SetBookmarkAsync(listing.Id, false)).Should().BeTrue();
        (await listings.GetDetailAsync(listing.Id))!.IsBookmarked.Should().BeFalse();
    }

    [Fact]
    public async Task ListingQueryService_DeleteAllAsync_clears_listings_and_cascades()
    {
        await using var root = BuildHost();
        await using var scope = root.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var listings = scope.ServiceProvider.GetRequiredService<IListingQueryService>();

        var watch = new Watch
        {
            Name = "w",
            ProviderId = "nepremicnine",
            SearchUrl = "https://www.nepremicnine.net/x/",
        };
        db.Watches.Add(watch);

        var listing = new Listing
        {
            ProviderId = "nepremicnine",
            ExternalId = "111",
            Url = "https://www.nepremicnine.net/oglasi/111/",
            Title = "Flat",
        };
        var other = new Listing
        {
            ProviderId = "nepremicnine",
            ExternalId = "222",
            Url = "https://www.nepremicnine.net/oglasi/222/",
            Title = "House",
        };
        db.Listings.AddRange(listing, other);
        await db.SaveChangesAsync();

        db.PriceObservations.Add(
            new PriceObservation
            {
                ListingId = listing.Id,
                Amount = 100_000,
                Currency = "EUR",
            }
        );
        db.WatchSightings.Add(new WatchSighting { WatchId = watch.Id, ListingId = listing.Id });
        db.Bookmarks.Add(new Bookmark { ListingId = listing.Id });
        await db.SaveChangesAsync();

        var deleted = await listings.DeleteAllAsync();
        deleted.Should().Be(2);

        (await db.Listings.CountAsync()).Should().Be(0);
        (await db.PriceObservations.CountAsync()).Should().Be(0);
        (await db.WatchSightings.CountAsync()).Should().Be(0);
        (await db.Bookmarks.CountAsync()).Should().Be(0);
        (await db.Watches.CountAsync()).Should().Be(1);

        (await listings.DeleteAllAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ScanRunQueryService_returns_recent_runs()
    {
        await using var root = BuildHost();
        await using var scope = root.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var scans = scope.ServiceProvider.GetRequiredService<IScanRunQueryService>();

        var watch = new Watch
        {
            Name = "Watch A",
            ProviderId = "nepremicnine",
            SearchUrl = "https://www.nepremicnine.net/z/",
        };
        db.Watches.Add(watch);
        await db.SaveChangesAsync();

        db.ScanRuns.Add(
            new ScanRun
            {
                WatchId = watch.Id,
                Status = ScanRunStatus.Succeeded,
                QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                NewCount = 2,
                PriceChangeCount = 1,
            }
        );
        db.ScanRuns.Add(
            new ScanRun
            {
                WatchId = watch.Id,
                Status = ScanRunStatus.Failed,
                QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                ErrorSummary = "boom",
            }
        );
        await db.SaveChangesAsync();

        var recent = await scans.GetRecentAsync(10);
        recent.Should().HaveCount(2);
        recent[0].Status.Should().Be(ScanRunStatus.Failed);
        recent[0].WatchName.Should().Be("Watch A");
        recent[0].ErrorSummary.Should().Be("boom");
        recent[1].NewCount.Should().Be(2);
    }

    [Fact]
    public async Task ScanRunQueryService_returns_active_runs_only()
    {
        await using var root = BuildHost();
        await using var scope = root.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var scans = scope.ServiceProvider.GetRequiredService<IScanRunQueryService>();

        var watch = new Watch
        {
            Name = "Watch B",
            ProviderId = "nepremicnine",
            SearchUrl = "https://www.nepremicnine.net/z/",
        };
        db.Watches.Add(watch);
        await db.SaveChangesAsync();

        db.ScanRuns.Add(
            new ScanRun
            {
                WatchId = watch.Id,
                Status = ScanRunStatus.Succeeded,
                QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            }
        );
        db.ScanRuns.Add(
            new ScanRun
            {
                WatchId = watch.Id,
                Status = ScanRunStatus.Queued,
                QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            }
        );
        db.ScanRuns.Add(
            new ScanRun
            {
                WatchId = watch.Id,
                Status = ScanRunStatus.Running,
                QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                StartedAt = DateTimeOffset.UtcNow.AddSeconds(-30),
            }
        );
        await db.SaveChangesAsync();

        var active = await scans.GetActiveAsync();
        active.Should().HaveCount(2);
        active.Select(r => r.Status).Should().Equal(ScanRunStatus.Queued, ScanRunStatus.Running);
    }

    [Fact]
    public void SettingsService_reports_capability_flags()
    {
        var empty = new SettingsService(Options.Create(new DomolovOptions()));
        var s = empty.GetSettings();
        s.TelegramConfigured.Should().BeFalse();
        s.SmtpConfigured.Should().BeFalse();
        s.VapidConfigured.Should().BeFalse();
        s.TimeZone.Should().Be("Europe/Ljubljana");
        s.MaxConcurrentScans.Should().Be(1);
        s.ScanCooldownMs.Should().Be(20_000);

        var full = new SettingsService(
            Options.Create(
                new DomolovOptions
                {
                    TelegramBotToken = "tok",
                    SmtpHost = "smtp.example",
                    VapidPublicKey = "pub",
                    VapidPrivateKey = "priv",
                    Role = "worker",
                    MaxConcurrentScans = 4,
                    ScanCooldownMs = 5_000,
                    BrowserHeadless = true,
                    TimeZone = "UTC",
                }
            )
        );
        var f = full.GetSettings();
        f.TelegramConfigured.Should().BeTrue();
        f.SmtpConfigured.Should().BeTrue();
        f.VapidConfigured.Should().BeTrue();
        f.VapidPublicKey.Should().Be("pub");
        f.Role.Should().Be("worker");
        f.MaxConcurrentScans.Should().Be(4);
        f.ScanCooldownMs.Should().Be(5_000);
        f.BrowserHeadless.Should().BeTrue();
        f.TimeZone.Should().Be("UTC");
    }

    private static ServiceProvider BuildHost()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DomolovDbContext>(o =>
            o.UseInMemoryDatabase("app-" + Guid.NewGuid())
        );
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DomolovDbContext>());
        services.AddSingleton<IListingProviderResolver>(new FixedResolver(new StubProvider()));
        services.AddScoped<IWatchService, WatchService>();
        services.AddScoped<IListingQueryService, ListingQueryService>();
        services.AddScoped<IScanRunQueryService, ScanRunQueryService>();
        services.AddSingleton(Options.Create(new DomolovOptions()));
        return services.BuildServiceProvider();
    }

    private sealed class StubProvider : IListingProvider
    {
        public string Id => NepremicnineParsing.ProviderId;

        public bool CanHandle(Uri searchUrl) => NepremicnineParsing.IsNepremicnineHost(searchUrl);

        public IAsyncEnumerable<ListingCard> CrawlAsync(
            CrawlRequest request,
            CancellationToken cancellationToken
        ) => AsyncEnumerable.Empty<ListingCard>();
    }

    private sealed class FixedResolver(IListingProvider provider) : IListingProviderResolver
    {
        public IListingProvider Resolve(Uri searchUrl) => provider;

        public IListingProvider? TryGetById(string providerId) => provider;
    }
}

internal static class WatchResponseExtensions
{
    public static UpdateWatchRequest ToUpdate(this WatchResponse watch) =>
        new()
        {
            Name = watch.Name,
            SearchUrl = watch.SearchUrl,
            Cron = watch.Cron,
            IsEnabled = watch.IsEnabled,
        };
}
