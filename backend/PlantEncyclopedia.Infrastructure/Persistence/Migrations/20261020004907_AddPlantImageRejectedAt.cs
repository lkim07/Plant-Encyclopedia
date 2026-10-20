using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantEncyclopedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlantImageRejectedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rejected_at",
                table: "plant_images",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rejected_at",
                table: "plant_images");
        }
    }
}
