using Microsoft.EntityFrameworkCore;

namespace Domolov.Infrastructure.Persistence.Configurations;

internal static class ClientGeneratedKeys
{
    /// <summary>
    /// Aggregates create their own UUID v7 ids. Without this, EF treats a new entity reached
    /// through a navigation (e.g. a PriceObservation added to a tracked Listing) as existing,
    /// because its key is already set, and issues an UPDATE instead of an INSERT.
    /// </summary>
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var key = entity.FindPrimaryKey();
            if (
                key is { Properties: [var property] }
                && property.ClrType == typeof(Guid)
                && !entity.IsOwned()
            )
            {
                property.ValueGenerated = Microsoft
                    .EntityFrameworkCore
                    .Metadata
                    .ValueGenerated
                    .Never;
            }
        }
    }
}
