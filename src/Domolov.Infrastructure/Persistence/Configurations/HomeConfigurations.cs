using Domolov.Domain.Homes;
using Domolov.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domolov.Infrastructure.Persistence.Configurations;

internal sealed class HomeConfiguration : IEntityTypeConfiguration<Home>
{
    public void Configure(EntityTypeBuilder<Home> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Title).HasMaxLength(Listing.TitleMaxLength).IsRequired();
        e.Property(x => x.ImageUrl).HasMaxLength(2000);
        e.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        e.Property(x => x.RoomCount).HasPrecision(5, 1);
        e.Property(x => x.SizeM2).HasPrecision(12, 2);
        e.Property(x => x.LandSizeM2).HasPrecision(12, 2);
        e.Property(x => x.CurrentPrice).HasPrecision(18, 2);
        e.Property(x => x.PreviousPrice).HasPrecision(18, 2);
        e.Property(x => x.PricePerM2).HasPrecision(18, 2);
        e.Property(x => x.Version).IsRowVersion();
        e.HasOne(x => x.Bookmark)
            .WithOne()
            .HasForeignKey<Bookmark>(x => x.HomeId)
            .OnDelete(DeleteBehavior.Cascade);
        e.Ignore(x => x.IsDismissed);
        e.Ignore(x => x.IsUnseen);
        e.Ignore(x => x.IsOffMarket);
        e.HasIndex(x => x.FirstSeenAt);
        e.HasIndex(x => x.PriceChangedAt);
        e.HasIndex(x => x.CurrentPrice);
        e.HasIndex(x => x.SeenAt);
        e.HasIndex(x => x.DismissedAt);
        e.HasIndex(x => x.OffMarketAt);
    }
}

internal sealed class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> e)
    {
        e.HasKey(x => x.HomeId);
        e.Property(x => x.Note).HasMaxLength(Bookmark.NoteMaxLength);
        e.Property(x => x.Version).IsRowVersion();
    }
}

internal sealed class HomeMatchConfiguration : IEntityTypeConfiguration<HomeMatch>
{
    public void Configure(EntityTypeBuilder<HomeMatch> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Home>().WithMany().HasForeignKey(x => x.HomeId).OnDelete(DeleteBehavior.Cascade);
        e.OwnsOne(x => x.Signals, s => s.ToJson());
        e.HasIndex(x => new { x.ListingId, x.HomeId });
        e.HasIndex(x => x.State);
    }
}
