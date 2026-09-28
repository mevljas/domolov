using Domolov.Application.Abstractions;
using Domolov.Domain.Auth;
using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Domolov.Domain.Notifications;
using Domolov.Domain.Push;
using Domolov.Domain.Retention;
using Domolov.Domain.Scans;
using Domolov.Domain.Watches;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Infrastructure.Persistence;

/// <summary>EF Core database context (PostgreSQL).</summary>
public sealed class DomolovDbContext(DbContextOptions<DomolovDbContext> options)
    : DbContext(options),
        IAppDbContext,
        IDataProtectionKeyContext
{
    public DbSet<Watch> Watches => Set<Watch>();
    public DbSet<NotificationRoute> NotificationRoutes => Set<NotificationRoute>();
    public DbSet<WatchSighting> WatchSightings => Set<WatchSighting>();
    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();
    public DbSet<ScanRunArtifact> ScanRunArtifacts => Set<ScanRunArtifact>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<PriceObservation> PriceObservations => Set<PriceObservation>();
    public DbSet<Home> Homes => Set<Home>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<HomeMatch> HomeMatches => Set<HomeMatch>();
    public DbSet<OperatorSession> OperatorSessions => Set<OperatorSession>();
    public DbSet<WebPushSubscription> WebPushSubscriptions => Set<WebPushSubscription>();
    public DbSet<CleanupRun> CleanupRuns => Set<CleanupRun>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DomolovDbContext).Assembly);
        Configurations.ClientGeneratedKeys.Apply(modelBuilder);
    }
}
