using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantEncyclopedia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Search support: the search_key() function (accents, case and punctuation removed) and
    /// trigram indexes on it for every searchable name column. No tables or data change.
    /// </summary>
    public partial class AddSearchSupport : Migration
    {
        // Whitespace and punctuation, listed explicitly (not [[:punct:]]) so the result does not depend on
        // the database locale: letters in any script are never removed, even under a "C" locale.
        // Code point ranges (first, last), written into the SQL as PostgreSQL regex escapes.
        private static readonly (int First, int Last)[] SeparatorRanges =
        [
            (0x21, 0x2F), (0x3A, 0x40), (0x5B, 0x60), (0x7B, 0x7E), // ASCII punctuation
            (0xA0, 0xA1), (0xAB, 0xAB), (0xB7, 0xB7), (0xBB, 0xBB), // no-break space, inverted !, guillemets, middle dot
            (0xBF, 0xBF), (0xD7, 0xD7), (0xF7, 0xF7), // inverted ?, multiplication and division signs
            (0x2010, 0x2027), (0x2030, 0x205E), // dashes, quotes, bullets, ellipsis, other general punctuation
            (0x3000, 0x3003), (0x3008, 0x3011), (0x3014, 0x301F), // CJK space and punctuation
            (0xFF01, 0xFF0F) // full-width ASCII punctuation
        ];

        // A bracket expression of whitespace plus every range above, one or more times.
        private static readonly string Separators =
            "[\\s" + string.Concat(SeparatorRanges.Select(r => $"\\u{r.First:X4}-\\u{r.Last:X4}")) + "]+";

        private static readonly (string Index, string Table, string Column)[] SearchIndexes =
        [
            ("ix_plants_display_name_search", "plants", "display_name"),
            ("ix_plants_scientific_name_search", "plants", "scientific_name"),
            ("ix_plants_cultivar_name_search", "plants", "cultivar_name"),
            ("ix_plant_names_name_search", "plant_names", "name"),
            ("ix_plant_groups_name_search", "plant_groups", "name")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            // unaccent() is STABLE because its dictionary could change; with the dictionary named explicitly
            // the result is fixed, so the wrapper may be IMMUTABLE and used in index expressions.
            // Every name is schema-qualified because index expressions must not depend on search_path.
            migrationBuilder.Sql($"""
                CREATE FUNCTION public.search_key(value text) RETURNS text
                LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
                RETURN pg_catalog.btrim(pg_catalog.regexp_replace(
                    pg_catalog.lower(public.unaccent('public.unaccent'::regdictionary, value)),
                    '{Separators}', ' ', 'g'));
                """);

            foreach (var (index, table, column) in SearchIndexes)
            {
                migrationBuilder.Sql(
                    $"CREATE INDEX {index} ON {table} USING gin (public.search_key({column}) public.gin_trgm_ops);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (index, _, _) in SearchIndexes)
            {
                migrationBuilder.Sql($"DROP INDEX {index};");
            }

            migrationBuilder.Sql("DROP FUNCTION public.search_key(text);");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");
        }
    }
}
