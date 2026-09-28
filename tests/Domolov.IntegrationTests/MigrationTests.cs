using Domolov.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Domolov.IntegrationTests;

/// <summary>Upgrading a v1 database keeps Watches, Listings, prices and Bookmarks.</summary>
[Collection(PostgresCollection.Name)]
public sealed class MigrationTests(PostgresFixture postgres)
{
    private const string LastV1Migration = "20260915183943_AddCloudflareBackoffAndScanFlags";

    [Fact]
    public async Task V1_data_is_carried_onto_homes_with_prices_and_bookmarks()
    {
        await using var app = await postgres.CreateAppAsync(migrate: false);
        await app.QueryAsync(async db =>
        {
            await db.GetService<IMigrator>().MigrateAsync(LastV1Migration);
            return 0;
        });

        var watchId = Guid.NewGuid();
        var listingId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(app.ConnectionString))
        {
            await connection.OpenAsync();
            var sql = $"""
                INSERT INTO "Watches" ("Id","Name","ProviderId","SearchUrl","Cron","IsEnabled","CreatedAt","HasCompletedBaseline","CloudflareStrikeCount")
                VALUES ('{watchId}','Old','nepremicnine','https://www.nepremicnine.net/oglasi-prodaja/','0 */6 * * *', false, '2026-08-01T00:00:00Z', true, 0);
                INSERT INTO "NotificationRoutes" ("Id","WatchId","Channel","Destination","Triggers","IsEnabled")
                VALUES ('{Guid.NewGuid()}','{watchId}',0,'https://discord.com/api/webhooks/1/a',3,true);
                INSERT INTO "Listings" ("Id","ProviderId","ExternalId","Url","Title","SizeText","Rooms","FirstSeenAt","LastSeenAt")
                VALUES ('{listingId}','nepremicnine','111','https://x/111','LJUBLJANA, CENTER','72.4 m2','3-sobno','2026-08-01T00:00:00Z','2026-08-10T00:00:00Z'),
                       ('{otherId}','nepremicnine','222','https://x/222','KAMNIK',NULL,NULL,'2026-08-01T00:00:00Z','2026-08-10T00:00:00Z');
                INSERT INTO "PriceObservations" ("Id","ListingId","Amount","Currency","ObservedAt")
                VALUES ('{Guid.NewGuid()}','{listingId}',300000,'EUR','2026-08-01T00:00:00Z'),
                       ('{Guid.NewGuid()}','{listingId}',289600,'EUR','2026-08-05T00:00:00Z');
                INSERT INTO "WatchSightings" ("WatchId","ListingId","FirstSeenAt","LastSeenAt")
                VALUES ('{watchId}','{listingId}','2026-08-01T00:00:00Z','2026-08-10T00:00:00Z');
                INSERT INTO "Bookmarks" ("ListingId","CreatedAt") VALUES ('{listingId}','2026-08-02T00:00:00Z');
                """;
            await using var cmd = new NpgsqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        await app.RunAsync<DatabaseMigrator>(m => m.MigrateAsync(CancellationToken.None));

        var watch = await app.QueryAsync(db =>
            db.Watches.Include(w => w.NotificationRoutes).SingleAsync()
        );
        watch.IsPaused.Should().BeTrue("a disabled v1 Watch becomes a Paused Watch");
        watch.SearchUrlChangedAt.Should().Be(watch.CreatedAt);
        watch
            .NotificationRoutes.Single()
            .Triggers.Should()
            .HaveFlag(Domain.Notifications.NotificationTrigger.Reposted);

        var listing = await app.QueryAsync(db => db.Listings.SingleAsync(l => l.Id == listingId));
        listing.HomeId.Should().Be(listingId);
        listing.CurrentPrice.Should().Be(289_600m);
        listing.PreviousPrice.Should().Be(300_000m);
        listing.SizeM2.Should().Be(72.4m);
        listing.RoomCount.Should().Be(3m);
        listing.PricePerM2.Should().Be(4000m);
        listing.NormalizedTitle.Should().Be("ljubljana center");

        var home = await app.QueryAsync(db =>
            db.Homes.Include(h => h.Bookmark).SingleAsync(h => h.Id == listingId)
        );
        home.PrimaryListingId.Should().Be(listingId);
        home.CurrentPrice.Should().Be(289_600m);
        home.IsUnseen.Should().BeFalse("upgrades must not flood the Unseen strip");
        home.Bookmark.Should().NotBeNull();
        home.Bookmark!.Stage.Should().Be(Domain.Homes.BookmarkStage.Interested);

        (await app.QueryAsync(db => db.Homes.CountAsync())).Should().Be(2);
        (await app.QueryAsync(db => db.Database.GetPendingMigrationsAsync())).Should().BeEmpty();
    }

    [Fact]
    public async Task Migrating_twice_is_a_no_op()
    {
        await using var app = await postgres.CreateAppAsync();
        await app.RunAsync<DatabaseMigrator>(m => m.MigrateAsync(CancellationToken.None));
        (await app.QueryAsync(db => db.Homes.CountAsync())).Should().Be(0);
    }
}
