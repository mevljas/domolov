using System.Runtime.CompilerServices;
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

public sealed class ScanOrchestratorTests
{
    [Fact]
    public async Task First_scan_is_baseline_without_notifications()
    {
        await using var root = BuildProvider(
            new FakeListingProvider([
                new ListingCard(
                    "1",
                    "https://example.com/1/",
                    "A",
                    100,
                    "EUR",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null
                ),
            ])
        );
        await using var scope = root.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var orchestrator = sp.GetRequiredService<IScanOrchestrator>();
        var db = sp.GetRequiredService<IAppDbContext>();

        var watch = new Watch
        {
            Name = "t",
            ProviderId = "fake",
            SearchUrl = "https://example.com/search",
        };
        db.Watches.Add(watch);
        await db.SaveChangesAsync();

        var runId = await orchestrator.EnqueueAsync(watch.Id);
        await orchestrator.ProcessQueuedAsync();

        var run = await db.ScanRuns.AsNoTracking().SingleAsync(r => r.Id == runId);
        run.Status.Should().Be(ScanRunStatus.Baseline);
        (await db.Listings.CountAsync()).Should().Be(1);
        FakeNotifier.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Second_scan_notifies_on_new_listing()
    {
        var cards = new List<ListingCard>
        {
            new(
                "1",
                "https://example.com/1/",
                "A",
                100,
                "EUR",
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
        };
        await using var root = BuildProvider(new FakeListingProvider(cards));
        await using var scope = root.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var orchestrator = sp.GetRequiredService<IScanOrchestrator>();
        var db = sp.GetRequiredService<IAppDbContext>();

        var watch = new Watch
        {
            Name = "t",
            ProviderId = "fake",
            SearchUrl = "https://example.com/search",
        };
        db.Watches.Add(watch);
        await db.SaveChangesAsync();

        await orchestrator.EnqueueAsync(watch.Id);
        await orchestrator.ProcessQueuedAsync();

        // Reload — ProcessQueuedAsync uses its own DI scopes.
        watch = await db.Watches.AsNoTracking().Include(w => w.NotificationRoutes).SingleAsync();
        watch.HasCompletedBaseline.Should().BeTrue();

        var tracked = await db.Watches.SingleAsync(w => w.Id == watch.Id);
        db.NotificationRoutes.Add(
            new NotificationRoute
            {
                WatchId = tracked.Id,
                Channel = NotificationChannel.Discord,
                Destination = "https://discord.test/webhook",
                Triggers = NotificationTrigger.NewListing,
            }
        );
        await db.SaveChangesAsync();

        cards.Add(
            new ListingCard(
                "2",
                "https://example.com/2/",
                "B",
                200,
                "EUR",
                null,
                null,
                null,
                null,
                null,
                null,
                null
            )
        );
        FakeNotifier.Sent.Clear();

        await orchestrator.EnqueueAsync(watch.Id);
        await orchestrator.ProcessQueuedAsync();

        FakeNotifier.Sent.Should().ContainSingle(m => m.Title == "B");
        (await db.ScanRuns.AsNoTracking().OrderByDescending(r => r.QueuedAt).FirstAsync())
            .Status.Should()
            .Be(ScanRunStatus.Succeeded);
    }

    private static ServiceProvider BuildProvider(IListingProvider listingProvider)
    {
        FakeNotifier.Sent.Clear();
        var dbName = "scan-" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DomolovDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DomolovDbContext>());
        services.AddSingleton(Options.Create(new DomolovOptions { MaxConcurrentScans = 2 }));
        services.AddSingleton(listingProvider);
        services.AddSingleton<IListingProviderResolver, ListingProviderResolverStub>();
        services.AddSingleton<INotifier, FakeNotifier>();
        services.AddSingleton<IScanOrchestrator, ScanOrchestrator>();
        return services.BuildServiceProvider();
    }

    private sealed class ListingProviderResolverStub(IListingProvider provider)
        : IListingProviderResolver
    {
        public IListingProvider Resolve(Uri searchUrl) => provider;

        public IListingProvider? TryGetById(string providerId) => provider;
    }

    private sealed class FakeListingProvider(IReadOnlyList<ListingCard> cards) : IListingProvider
    {
        public string Id => "fake";

        public bool CanHandle(Uri searchUrl) => true;

        public async IAsyncEnumerable<ListingCard> CrawlAsync(
            CrawlRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            foreach (var card in cards)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return card;
                await Task.Yield();
            }
        }
    }

    private sealed class FakeNotifier : INotifier
    {
        public static List<NotificationMessage> Sent { get; } = [];

        public NotificationChannel Channel => NotificationChannel.Discord;

        public Task SendAsync(
            NotificationMessage message,
            CancellationToken cancellationToken = default
        )
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}

public sealed class WatchServiceTests
{
    [Fact]
    public async Task Create_resolves_provider_from_url()
    {
        var services = new ServiceCollection();
        services.AddDbContext<DomolovDbContext>(o =>
            o.UseInMemoryDatabase(Guid.NewGuid().ToString())
        );
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DomolovDbContext>());
        services.AddSingleton<IListingProviderResolver>(new FixedResolver(new StubProvider()));
        services.AddScoped<IWatchService, WatchService>();
        await using var root = services.BuildServiceProvider();
        await using var scope = root.CreateAsyncScope();
        var watches = scope.ServiceProvider.GetRequiredService<IWatchService>();

        var created = await watches.CreateAsync(
            new CreateWatchRequest
            {
                Name = "Demo",
                SearchUrl = "https://www.nepremicnine.net/oglasi-prodaja/",
            }
        );

        created.ProviderId.Should().Be("nepremicnine");
        created.Name.Should().Be("Demo");
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

internal static class AsyncEnumerable
{
    public static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
