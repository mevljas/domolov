using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domolov.Infrastructure.Persistence.Configurations;

internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> e)
    {
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.ProviderId, x.ExternalId }).IsUnique();
        e.HasOne<Home>().WithMany().HasForeignKey(x => x.HomeId).OnDelete(DeleteBehavior.Cascade);
        e.Property(x => x.ProviderId).HasMaxLength(64).IsRequired();
        e.Property(x => x.ExternalId).HasMaxLength(128).IsRequired();
        e.Property(x => x.Title).HasMaxLength(Listing.TitleMaxLength).IsRequired();
        e.Property(x => x.Url).HasMaxLength(2000).IsRequired();
        e.Property(x => x.ImageUrl).HasMaxLength(2000);
        e.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        e.Property(x => x.SizeM2).HasPrecision(12, 2);
        e.Property(x => x.LandSizeM2).HasPrecision(12, 2);
        e.Property(x => x.RoomCount).HasPrecision(5, 1);
        e.Property(x => x.CurrentPrice).HasPrecision(18, 2);
        e.Property(x => x.PreviousPrice).HasPrecision(18, 2);
        e.Property(x => x.PricePerM2).HasPrecision(18, 2);
        e.HasMany(x => x.Prices)
            .WithOne()
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
        e.Navigation(x => x.Prices).UsePropertyAccessMode(PropertyAccessMode.Field);
        e.Ignore(x => x.IsDelisted);
        e.HasIndex(x => x.HomeId);
        e.HasIndex(x => x.DelistedAt);
        e.HasIndex(x => new
        {
            x.PropertyType,
            x.RoomCount,
            x.SizeM2,
        });
    }
}

internal sealed class PriceObservationConfiguration : IEntityTypeConfiguration<PriceObservation>
{
    public void Configure(EntityTypeBuilder<PriceObservation> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Amount).HasPrecision(18, 2);
        e.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        e.HasIndex(x => new { x.ListingId, x.ObservedAt });
    }
}
