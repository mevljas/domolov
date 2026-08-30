using Domolov.Application.Abstractions;
using Domolov.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Infrastructure.Persistence;

/// <summary>EF Core database context.</summary>
public sealed class DomolovDbContext(DbContextOptions<DomolovDbContext> options)
    : DbContext(options),
        IAppDbContext
{
    public DbSet<Watch> Watches => Set<Watch>();
    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<PriceObservation> PriceObservations => Set<PriceObservation>();
    public DbSet<WatchSighting> WatchSightings => Set<WatchSighting>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<NotificationRoute> NotificationRoutes => Set<NotificationRoute>();
    public DbSet<PushSubscriptionEntity> PushSubscriptions => Set<PushSubscriptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Watch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.ProviderId).HasMaxLength(64).IsRequired();
            e.Property(x => x.SearchUrl).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Cron).HasMaxLength(100).IsRequired();
            e.HasMany(x => x.NotificationRoutes)
                .WithOne(x => x.Watch!)
                .HasForeignKey(x => x.WatchId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ScanRuns)
                .WithOne(x => x.Watch!)
                .HasForeignKey(x => x.WatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Listing>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProviderId, x.ExternalId }).IsUnique();
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Url).HasMaxLength(2000).IsRequired();
            e.Property(x => x.ExternalId).HasMaxLength(128).IsRequired();
            e.HasMany(x => x.Prices)
                .WithOne(x => x.Listing!)
                .HasForeignKey(x => x.ListingId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Bookmark)
                .WithOne(x => x.Listing!)
                .HasForeignKey<Bookmark>(x => x.ListingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PriceObservation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.ListingId, x.ObservedAt });
        });

        modelBuilder.Entity<WatchSighting>(e =>
        {
            e.HasKey(x => new { x.WatchId, x.ListingId });
            e.HasOne(x => x.Watch!)
                .WithMany(x => x.Sightings)
                .HasForeignKey(x => x.WatchId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Listing!)
                .WithMany(x => x.Sightings)
                .HasForeignKey(x => x.ListingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Bookmark>(e => e.HasKey(x => x.ListingId));

        modelBuilder.Entity<NotificationRoute>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Destination).HasMaxLength(2000).IsRequired();
        });

        modelBuilder.Entity<PushSubscriptionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Endpoint).IsUnique();
            e.Property(x => x.Endpoint).HasMaxLength(2000).IsRequired();
        });

        modelBuilder.Entity<ScanRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WatchId, x.Status });
        });
    }
}
