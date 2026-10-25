using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantEncyclopedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "search_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    query = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    query_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    searched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_search_history_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "view_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_view_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_view_history_plants_plant_id",
                        column: x => x.plant_id,
                        principalTable: "plants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_view_history_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_search_history_user_searched_at",
                table: "search_history",
                columns: new[] { "user_id", "searched_at" });

            migrationBuilder.CreateIndex(
                name: "ux_search_history_user_query_key",
                table: "search_history",
                columns: new[] { "user_id", "query_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_view_history_plant_id",
                table: "view_history",
                column: "plant_id");

            migrationBuilder.CreateIndex(
                name: "ix_view_history_user_viewed_at",
                table: "view_history",
                columns: new[] { "user_id", "viewed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_history");

            migrationBuilder.DropTable(
                name: "view_history");
        }
    }
}
