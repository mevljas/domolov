using Domolov.Domain.Auth;
using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Domolov.Domain.Notifications;
using Domolov.Domain.Push;
using Domolov.Domain.Retention;
using Domolov.Domain.Scans;
using Domolov.Domain.Watches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Domolov.Application.Abstractions;

/// <summary>EF Core application database boundary.</summary>
public interface IAppDbContext
{
    DbSet<Watch> Watches { get; }
    DbSet<NotificationRoute> NotificationRoutes { get; }
    DbSet<WatchSighting> WatchSightings { get; }
    DbSet<ScanRun> ScanRuns { get; }
    DbSet<ScanRunArtifact> ScanRunArtifacts { get; }
    DbSet<Listing> Listings { get; }
    DbSet<PriceObservation> PriceObservations { get; }
    DbSet<Home> Homes { get; }
    DbSet<Bookmark> Bookmarks { get; }
    DbSet<HomeMatch> HomeMatches { get; }
    DbSet<OperatorSession> OperatorSessions { get; }
    DbSet<WebPushSubscription> WebPushSubscriptions { get; }
    DbSet<CleanupRun> CleanupRuns { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
