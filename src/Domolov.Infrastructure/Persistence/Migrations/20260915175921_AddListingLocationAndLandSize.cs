using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domolov.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListingLocationAndLandSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LandSizeText",
                table: "Listings",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Listings",
                type: "text",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LandSizeText", table: "Listings");

            migrationBuilder.DropColumn(name: "Location", table: "Listings");
        }
    }
}
