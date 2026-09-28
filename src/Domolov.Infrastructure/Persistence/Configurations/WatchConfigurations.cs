using Domolov.Domain.Listings;
using Domolov.Domain.Notifications;
using Domolov.Domain.Scans;
using Domolov.Domain.Watches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domolov.Infrastructure.Persistence.Configurations;

internal sealed class WatchConfiguration : IEntityTypeConfiguration<Watch>
{
    public void Configure(EntityTypeBuilder<Watch> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(Watch.NameMaxLength).IsRequired();
        e.Property(x => x.ProviderId).HasMaxLength(64).IsRequired();
        e.Property(x => x.SearchUrl).HasMaxLength(Watch.SearchUrlMaxLength).IsRequired();
        e.Property(x => x.Cron).HasMaxLength(100).IsRequired();
        e.Property(x => x.Version).IsRowVersion();
        e.HasMany(x => x.NotificationRoutes)
            .WithOne()
            .HasForeignKey(x => x.WatchId)
            .OnDelete(DeleteBehavior.Cascade);
        e.Navigation(x => x.NotificationRoutes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class NotificationRouteConfiguration : IEntityTypeConfiguration<NotificationRoute>
{
    public void Configure(EntityTypeBuilder<NotificationRoute> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Destination)
            .HasMaxLength(NotificationRoute.DestinationMaxLength)
            .IsRequired();
        e.Property(x => x.Version).IsRowVersion();
    }
}

internal sealed class WatchSightingConfiguration : IEntityTypeConfiguration<WatchSighting>
{
    public void Configure(EntityTypeBuilder<WatchSighting> e)
    {
        e.HasKey(x => new { x.WatchId, x.ListingId });
        e.HasOne<Watch>().WithMany().HasForeignKey(x => x.WatchId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.ListingId);
    }
}

internal sealed class ScanRunConfiguration : IEntityTypeConfiguration<ScanRun>
{
    public void Configure(EntityTypeBuilder<ScanRun> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne(x => x.Watch)
            .WithMany()
            .HasForeignKey(x => x.WatchId)
            .OnDelete(DeleteBehavior.Cascade);
        e.Property(x => x.ErrorSummary).HasMaxLength(ScanRun.ErrorMaxLength);
        e.Property(x => x.NotifyErrorSummary).HasMaxLength(ScanRun.ErrorMaxLength);
        e.HasIndex(x => new { x.WatchId, x.Status });
        e.HasIndex(x => x.QueuedAt);
    }
}

internal sealed class ScanRunArtifactConfiguration : IEntityTypeConfiguration<ScanRunArtifact>
{
    public void Configure(EntityTypeBuilder<ScanRunArtifact> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<ScanRun>()
            .WithMany()
            .HasForeignKey(x => x.ScanRunId)
            .OnDelete(DeleteBehavior.Cascade);
        e.Property(x => x.Label).HasMaxLength(100).IsRequired();
        e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.ScanRunId);
        e.HasIndex(x => x.CreatedAt);
    }
}
