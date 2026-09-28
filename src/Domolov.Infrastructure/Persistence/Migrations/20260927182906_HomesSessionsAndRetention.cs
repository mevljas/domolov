using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Domolov.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HomesSessionsAndRetention : Migration
    {
        /// <summary>
        /// Moves v1 data onto the v2 model: one Home per existing Listing (same id, so Bookmarks
        /// keep pointing at the right row), current/previous prices from the price history, and
        /// sensible values for new timestamps. Existing Homes start as seen so an upgrade does not
        /// flood the Unseen strip. Parsed attributes and Home denormalisation are backfilled by
        /// the <c>migrate</c> command afterwards.
        /// </summary>
        private const string MigrateExistingData = """
            UPDATE "Watches" SET "SearchUrlChangedAt" = "CreatedAt";

            UPDATE "Bookmarks" SET "UpdatedAt" = "CreatedAt";

            UPDATE "NotificationRoutes" SET "Triggers" = "Triggers" | 16;

            WITH ranked AS (
                SELECT "ListingId", "Amount", "Currency", "ObservedAt",
                       ROW_NUMBER() OVER (PARTITION BY "ListingId" ORDER BY "ObservedAt" DESC) AS rn
                FROM "PriceObservations"
            )
            UPDATE "Listings" l
            SET "CurrentPrice" = cur."Amount",
                "Currency" = cur."Currency",
                "PreviousPrice" = prev."Amount",
                "PriceChangedAt" = CASE WHEN prev."Amount" IS NOT NULL THEN cur."ObservedAt" END
            FROM ranked cur
            LEFT JOIN ranked prev ON prev."ListingId" = cur."ListingId" AND prev.rn = 2
            WHERE cur."ListingId" = l."Id" AND cur.rn = 1;

            UPDATE "Listings" SET "Currency" = 'EUR' WHERE "Currency" = '';

            UPDATE "Listings" SET "HomeId" = "Id";

            INSERT INTO "Homes" ("Id", "PrimaryListingId", "SeenAt", "FirstSeenAt", "LastSeenAt",
                                 "Title", "Currency", "ListingCount", "ActiveListingCount", "RepostCount")
            SELECT l."Id", NULL, now(), l."FirstSeenAt", l."LastSeenAt", l."Title", l."Currency", 1, 1, 0
            FROM "Listings" l;

            DELETE FROM "Bookmarks" b WHERE NOT EXISTS (SELECT 1 FROM "Homes" h WHERE h."Id" = b."HomeId");
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_Listings_ListingId",
                table: "Bookmarks"
            );

            migrationBuilder.RenameColumn(name: "IsEnabled", table: "Watches", newName: "IsPaused");

            // A disabled Watch is now a Paused Watch.
            migrationBuilder.Sql("""UPDATE "Watches" SET "IsPaused" = NOT "IsPaused";""");

            migrationBuilder.RenameColumn(name: "ListingId", table: "Bookmarks", newName: "HomeId");

            migrationBuilder.AddColumn<bool>(
                name: "IsStale",
                table: "WatchSightings",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "MissedRunCount",
                table: "WatchSightings",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SearchUrlChangedAt",
                table: "Watches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(
                    new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    new TimeSpan(0, 0, 0, 0, 0)
                )
            );

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Watches",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u
            );

            migrationBuilder.AlterColumn<string>(
                name: "NotifyErrorSummary",
                table: "ScanRuns",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "ErrorSummary",
                table: "ScanRuns",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "CrawlReachedEnd",
                table: "ScanRuns",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsManual",
                table: "ScanRuns",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "RepostCount",
                table: "ScanRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AlterColumn<string>(
                name: "P256dh",
                table: "PushSubscriptions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AlterColumn<string>(
                name: "Auth",
                table: "PushSubscriptions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "PushSubscriptions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "PriceObservations",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "NotificationRoutes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u
            );

            migrationBuilder.AlterColumn<string>(
                name: "ProviderId",
                table: "Listings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Listings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Listings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentPrice",
                table: "Listings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DelistedAt",
                table: "Listings",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "HomeId",
                table: "Listings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<long>(
                name: "ImageHash",
                table: "Listings",
                type: "bigint",
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "LandSizeM2",
                table: "Listings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "NormalizedDescription",
                table: "Listings",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "NormalizedTitle",
                table: "Listings",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousPrice",
                table: "Listings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PriceChangedAt",
                table: "Listings",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerM2",
                table: "Listings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "RoomCount",
                table: "Listings",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "SizeM2",
                table: "Listings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "YearBuilt",
                table: "Listings",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Bookmarks",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "Bookmarks",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Bookmarks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(
                    new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    new TimeSpan(0, 0, 0, 0, 0)
                )
            );

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Bookmarks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u
            );

            migrationBuilder.CreateTable(
                name: "CleanupRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsManual = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    FinishedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    DeletedListings = table.Column<int>(type: "integer", nullable: false),
                    DeletedHomes = table.Column<int>(type: "integer", nullable: false),
                    DeletedScanRuns = table.Column<int>(type: "integer", nullable: false),
                    DeletedArtifacts = table.Column<int>(type: "integer", nullable: false),
                    DeletedSessions = table.Column<int>(type: "integer", nullable: false),
                    BrowserProfileBytes = table.Column<long>(type: "bigint", nullable: true),
                    Error = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanupRuns", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Homes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    DismissedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    SeenAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    FirstSeenAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    LastSeenAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    OffMarketAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Title = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    ImageUrl = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                    Location = table.Column<string>(type: "text", nullable: true),
                    PropertyType = table.Column<string>(type: "text", nullable: true),
                    RoomCount = table.Column<decimal>(
                        type: "numeric(5,1)",
                        precision: 5,
                        scale: 1,
                        nullable: true
                    ),
                    SizeM2 = table.Column<decimal>(
                        type: "numeric(12,2)",
                        precision: 12,
                        scale: 2,
                        nullable: true
                    ),
                    LandSizeM2 = table.Column<decimal>(
                        type: "numeric(12,2)",
                        precision: 12,
                        scale: 2,
                        nullable: true
                    ),
                    CurrentPrice = table.Column<decimal>(
                        type: "numeric(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: true
                    ),
                    PreviousPrice = table.Column<decimal>(
                        type: "numeric(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: true
                    ),
                    Currency = table.Column<string>(
                        type: "character varying(8)",
                        maxLength: 8,
                        nullable: false
                    ),
                    PriceChangedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    PricePerM2 = table.Column<decimal>(
                        type: "numeric(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: true
                    ),
                    ListingCount = table.Column<int>(type: "integer", nullable: false),
                    ActiveListingCount = table.Column<int>(type: "integer", nullable: false),
                    RepostCount = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Homes", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "OperatorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    LastSeenAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ExpiresAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    RevokedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    UserAgent = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                    IpAddress = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSessions", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ScanRunArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    ContentType = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanRunArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanRunArtifacts_ScanRuns_ScanRunId",
                        column: x => x.ScanRunId,
                        principalTable: "ScanRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "HomeMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ReviewedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Signals = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeMatches_Homes_HomeId",
                        column: x => x.HomeId,
                        principalTable: "Homes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_HomeMatches_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ScanRuns_QueuedAt",
                table: "ScanRuns",
                column: "QueuedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Listings_DelistedAt",
                table: "Listings",
                column: "DelistedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Listings_HomeId",
                table: "Listings",
                column: "HomeId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Listings_PropertyType_RoomCount_SizeM2",
                table: "Listings",
                columns: new[] { "PropertyType", "RoomCount", "SizeM2" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CleanupRuns_StartedAt",
                table: "CleanupRuns",
                column: "StartedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_HomeMatches_HomeId",
                table: "HomeMatches",
                column: "HomeId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_HomeMatches_ListingId_HomeId",
                table: "HomeMatches",
                columns: new[] { "ListingId", "HomeId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_HomeMatches_State",
                table: "HomeMatches",
                column: "State"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Homes_CurrentPrice",
                table: "Homes",
                column: "CurrentPrice"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Homes_DismissedAt",
                table: "Homes",
                column: "DismissedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Homes_FirstSeenAt",
                table: "Homes",
                column: "FirstSeenAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Homes_OffMarketAt",
                table: "Homes",
                column: "OffMarketAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Homes_PriceChangedAt",
                table: "Homes",
                column: "PriceChangedAt"
            );

            migrationBuilder.CreateIndex(name: "IX_Homes_SeenAt", table: "Homes", column: "SeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSessions_ExpiresAt",
                table: "OperatorSessions",
                column: "ExpiresAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ScanRunArtifacts_CreatedAt",
                table: "ScanRunArtifacts",
                column: "CreatedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ScanRunArtifacts_ScanRunId",
                table: "ScanRunArtifacts",
                column: "ScanRunId"
            );

            migrationBuilder.Sql(MigrateExistingData);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_Homes_HomeId",
                table: "Bookmarks",
                column: "HomeId",
                principalTable: "Homes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_Homes_HomeId",
                table: "Listings",
                column: "HomeId",
                principalTable: "Homes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Bookmarks_Homes_HomeId", table: "Bookmarks");

            migrationBuilder.DropForeignKey(name: "FK_Listings_Homes_HomeId", table: "Listings");

            migrationBuilder.DropTable(name: "CleanupRuns");

            migrationBuilder.DropTable(name: "DataProtectionKeys");

            migrationBuilder.DropTable(name: "HomeMatches");

            migrationBuilder.DropTable(name: "OperatorSessions");

            migrationBuilder.DropTable(name: "ScanRunArtifacts");

            migrationBuilder.DropTable(name: "Homes");

            migrationBuilder.DropIndex(name: "IX_ScanRuns_QueuedAt", table: "ScanRuns");

            migrationBuilder.DropIndex(name: "IX_Listings_DelistedAt", table: "Listings");

            migrationBuilder.DropIndex(name: "IX_Listings_HomeId", table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_PropertyType_RoomCount_SizeM2",
                table: "Listings"
            );

            migrationBuilder.DropColumn(name: "IsStale", table: "WatchSightings");

            migrationBuilder.DropColumn(name: "MissedRunCount", table: "WatchSightings");

            migrationBuilder.DropColumn(name: "SearchUrlChangedAt", table: "Watches");

            migrationBuilder.DropColumn(name: "xmin", table: "Watches");

            migrationBuilder.DropColumn(name: "CrawlReachedEnd", table: "ScanRuns");

            migrationBuilder.DropColumn(name: "IsManual", table: "ScanRuns");

            migrationBuilder.DropColumn(name: "RepostCount", table: "ScanRuns");

            migrationBuilder.DropColumn(name: "UserAgent", table: "PushSubscriptions");

            migrationBuilder.DropColumn(name: "xmin", table: "NotificationRoutes");

            migrationBuilder.DropColumn(name: "Currency", table: "Listings");

            migrationBuilder.DropColumn(name: "CurrentPrice", table: "Listings");

            migrationBuilder.DropColumn(name: "DelistedAt", table: "Listings");

            migrationBuilder.DropColumn(name: "HomeId", table: "Listings");

            migrationBuilder.DropColumn(name: "ImageHash", table: "Listings");

            migrationBuilder.DropColumn(name: "LandSizeM2", table: "Listings");

            migrationBuilder.DropColumn(name: "NormalizedDescription", table: "Listings");

            migrationBuilder.DropColumn(name: "NormalizedTitle", table: "Listings");

            migrationBuilder.DropColumn(name: "PreviousPrice", table: "Listings");

            migrationBuilder.DropColumn(name: "PriceChangedAt", table: "Listings");

            migrationBuilder.DropColumn(name: "PricePerM2", table: "Listings");

            migrationBuilder.DropColumn(name: "RoomCount", table: "Listings");

            migrationBuilder.DropColumn(name: "SizeM2", table: "Listings");

            migrationBuilder.DropColumn(name: "YearBuilt", table: "Listings");

            migrationBuilder.DropColumn(name: "Note", table: "Bookmarks");

            migrationBuilder.DropColumn(name: "Stage", table: "Bookmarks");

            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Bookmarks");

            migrationBuilder.DropColumn(name: "xmin", table: "Bookmarks");

            migrationBuilder.RenameColumn(name: "IsPaused", table: "Watches", newName: "IsEnabled");

            migrationBuilder.RenameColumn(name: "HomeId", table: "Bookmarks", newName: "ListingId");

            migrationBuilder.AlterColumn<string>(
                name: "NotifyErrorSummary",
                table: "ScanRuns",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "ErrorSummary",
                table: "ScanRuns",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "P256dh",
                table: "PushSubscriptions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512
            );

            migrationBuilder.AlterColumn<string>(
                name: "Auth",
                table: "PushSubscriptions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512
            );

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "PriceObservations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8
            );

            migrationBuilder.AlterColumn<string>(
                name: "ProviderId",
                table: "Listings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64
            );

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Listings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_Listings_ListingId",
                table: "Bookmarks",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
