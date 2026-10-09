using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantEncyclopedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.CheckConstraint("ck_categories_slug", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateTable(
                name: "taxonomies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kingdom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phylum = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    @class = table.Column<string>(name: "class", type: "character varying(100)", maxLength: 100, nullable: true),
                    taxon_order = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    genus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    species = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subspecies = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    variety = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_taxonomies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plant_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_groups", x => x.id);
                    table.CheckConstraint("ck_plant_groups_slug", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_plant_groups_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scientific_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cultivar_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    plant_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    taxonomy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plants", x => x.id);
                    table.CheckConstraint("ck_plants_published_requires_group", "status <> 'Published' OR plant_group_id IS NOT NULL");
                    table.CheckConstraint("ck_plants_status", "status IN ('Draft', 'Validated', 'Published', 'NeedsReview', 'Archived')");
                    table.ForeignKey(
                        name: "fk_plants_plant_groups_plant_group_id",
                        column: x => x.plant_group_id,
                        principalTable: "plant_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plants_taxonomies_taxonomy_id",
                        column: x => x.taxonomy_id,
                        principalTable: "taxonomies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plant_care",
                columns: table => new
                {
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    light_summary = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    light_details = table.Column<string>(type: "text", nullable: true),
                    water_summary = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    water_details = table.Column<string>(type: "text", nullable: true),
                    temperature_min_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    temperature_max_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    temperature_details = table.Column<string>(type: "text", nullable: true),
                    soil_summary = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    soil_details = table.Column<string>(type: "text", nullable: true),
                    verification_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_care", x => x.plant_id);
                    table.CheckConstraint("ck_plant_care_temp_max_range", "temperature_max_c IS NULL OR temperature_max_c BETWEEN -60 AND 60");
                    table.CheckConstraint("ck_plant_care_temp_min_range", "temperature_min_c IS NULL OR temperature_min_c BETWEEN -60 AND 60");
                    table.CheckConstraint("ck_plant_care_temp_order", "temperature_min_c IS NULL OR temperature_max_c IS NULL OR temperature_min_c <= temperature_max_c");
                    table.CheckConstraint("ck_plant_care_verification_status", "verification_status IN ('Unverified', 'Verified')");
                    table.CheckConstraint("ck_plant_care_verified_at", "verification_status <> 'Verified' OR verified_at IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_plant_care_plants_plant_id",
                        column: x => x.plant_id,
                        principalTable: "plants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plant_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    thumbnail_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    medium_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    large_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    alt_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    license = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attribution = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_images", x => x.id);
                    table.CheckConstraint("ck_plant_images_height", "height IS NULL OR height > 0");
                    table.CheckConstraint("ck_plant_images_width", "width IS NULL OR width > 0");
                    table.ForeignKey(
                        name: "fk_plant_images_plants_plant_id",
                        column: x => x.plant_id,
                        principalTable: "plants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plant_names",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    language_code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_names", x => x.id);
                    table.CheckConstraint("ck_plant_names_common_language", "name_type <> 'Common' OR language_code IS NOT NULL");
                    table.CheckConstraint("ck_plant_names_language_code_shape", "language_code IS NULL OR language_code ~ '^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$'");
                    table.CheckConstraint("ck_plant_names_name_type", "name_type IN ('Common', 'Synonym')");
                    table.ForeignKey(
                        name: "fk_plant_names_plants_plant_id",
                        column: x => x.plant_id,
                        principalTable: "plants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plant_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_sources", x => x.id);
                    table.CheckConstraint("ck_plant_sources_identifier", "external_id IS NOT NULL OR source_url IS NOT NULL");
                    table.CheckConstraint("ck_plant_sources_source_type", "source_type IN ('Taxonomy', 'Description', 'Care', 'Image', 'Blooming', 'Health', 'About')");
                    table.ForeignKey(
                        name: "fk_plant_sources_plants_plant_id",
                        column: x => x.plant_id,
                        principalTable: "plants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_categories_name",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_categories_slug",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plant_groups_category_id",
                table: "plant_groups",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_plant_groups_slug",
                table: "plant_groups",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plant_images_plant_id",
                table: "plant_images",
                column: "plant_id");

            migrationBuilder.CreateIndex(
                name: "ux_plant_names_plant_type_name_language",
                table: "plant_names",
                columns: new[] { "plant_id", "name_type", "name", "language_code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_plant_sources_plant_id",
                table: "plant_sources",
                column: "plant_id");

            migrationBuilder.CreateIndex(
                name: "ux_plant_sources_external_id",
                table: "plant_sources",
                columns: new[] { "plant_id", "provider", "external_id", "source_type" },
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_plant_sources_source_url",
                table: "plant_sources",
                columns: new[] { "plant_id", "provider", "source_url", "source_type" },
                unique: true,
                filter: "external_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_plants_display_name",
                table: "plants",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "ix_plants_plant_group_id",
                table: "plants",
                column: "plant_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_plants_scientific_name",
                table: "plants",
                column: "scientific_name");

            migrationBuilder.CreateIndex(
                name: "ix_plants_taxonomy_id",
                table: "plants",
                column: "taxonomy_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plant_care");

            migrationBuilder.DropTable(
                name: "plant_images");

            migrationBuilder.DropTable(
                name: "plant_names");

            migrationBuilder.DropTable(
                name: "plant_sources");

            migrationBuilder.DropTable(
                name: "plants");

            migrationBuilder.DropTable(
                name: "plant_groups");

            migrationBuilder.DropTable(
                name: "taxonomies");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
