using Domolov.Domain.Auth;
using Domolov.Domain.Push;
using Domolov.Domain.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domolov.Infrastructure.Persistence.Configurations;

internal sealed class OperatorSessionConfiguration : IEntityTypeConfiguration<OperatorSession>
{
    public void Configure(EntityTypeBuilder<OperatorSession> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.UserAgent).HasMaxLength(512);
        e.Property(x => x.IpAddress).HasMaxLength(64);
        e.HasIndex(x => x.ExpiresAt);
    }
}

internal sealed class WebPushSubscriptionConfiguration
    : IEntityTypeConfiguration<WebPushSubscription>
{
    public void Configure(EntityTypeBuilder<WebPushSubscription> e)
    {
        e.ToTable("PushSubscriptions");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.Endpoint).IsUnique();
        e.Property(x => x.Endpoint).HasMaxLength(2000).IsRequired();
        e.Property(x => x.P256dh).HasMaxLength(512).IsRequired();
        e.Property(x => x.Auth).HasMaxLength(512).IsRequired();
        e.Property(x => x.UserAgent).HasMaxLength(512);
    }
}

internal sealed class CleanupRunConfiguration : IEntityTypeConfiguration<CleanupRun>
{
    public void Configure(EntityTypeBuilder<CleanupRun> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Error).HasMaxLength(2000);
        e.HasIndex(x => x.StartedAt);
    }
}
