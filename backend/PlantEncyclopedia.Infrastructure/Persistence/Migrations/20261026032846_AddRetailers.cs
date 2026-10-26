using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantEncyclopedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetailers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retailers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    postcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retailers", x => x.id);
                    table.CheckConstraint("ck_retailers_kind", "kind IN ('Nursery', 'GardenCentre', 'FlowerShop')");
                    table.CheckConstraint("ck_retailers_latitude", "latitude BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_retailers_longitude", "longitude BETWEEN -180 AND 180");
                });

            migrationBuilder.CreateIndex(
                name: "ix_retailers_latitude",
                table: "retailers",
                column: "latitude");

            migrationBuilder.CreateIndex(
                name: "ux_retailers_source_id",
                table: "retailers",
                columns: new[] { "source", "source_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retailers");
        }
    }
}
