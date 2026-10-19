using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantEncyclopedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlantImageKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "source_id",
                table: "plant_images",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "plant_images",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Photo");

            migrationBuilder.CreateIndex(
                name: "ux_plant_images_plant_source",
                table: "plant_images",
                columns: new[] { "plant_id", "source", "source_id" },
                unique: true,
                filter: "source_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plant_images_kind",
                table: "plant_images",
                sql: "kind IN ('Photo', 'Illustration')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_plant_images_plant_source",
                table: "plant_images");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plant_images_kind",
                table: "plant_images");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "plant_images");

            migrationBuilder.AlterColumn<string>(
                name: "source_id",
                table: "plant_images",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
