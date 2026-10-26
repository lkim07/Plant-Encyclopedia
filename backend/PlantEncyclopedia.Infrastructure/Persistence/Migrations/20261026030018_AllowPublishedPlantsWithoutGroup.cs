using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantEncyclopedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowPublishedPlantsWithoutGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_plants_published_requires_group",
                table: "plants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_plants_published_requires_group",
                table: "plants",
                sql: "status <> 'Published' OR plant_group_id IS NOT NULL");
        }
    }
}
