using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantEncyclopedia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Plant identity (DATABASE_DESIGN §59): a unique index on the normalized scientific name plus the
    /// normalized cultivar name, so the same plant cannot be stored twice (for example by two concurrent
    /// imports). No tables or data change.
    /// </summary>
    public partial class AddPlantIdentityKey : Migration
    {
        // Unicode whitespace as code point ranges (first, last), written into the SQL as regex escapes:
        // no-break space, Ogham space, en quad..hair space, line/paragraph separators,
        // narrow no-break space, medium mathematical space, ideographic space. \s covers ASCII whitespace.
        private static readonly (int First, int Last)[] WhitespaceRanges =
        [
            (0x00A0, 0x00A0), (0x1680, 0x1680), (0x2000, 0x200A), (0x2028, 0x2029),
            (0x202F, 0x202F), (0x205F, 0x205F), (0x3000, 0x3000)
        ];

        private static readonly string Whitespace =
            "[\\s" + string.Concat(WhitespaceRanges.Select(r => $"\\u{r.First:X4}-\\u{r.Last:X4}")) + "]+";

        // The multiplication sign U+00D7 marks a hybrid ("Rosa × hybrida").
        private static readonly string HybridSign = ((char)0x00D7).ToString();

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Scientific name: NFC; the hybrid sign becomes a separate "x" word (unaccent would turn it
            // into "*"); accents removed (botanical names do not use diacritics); ICU lower-case, so the
            // result does not depend on the database locale; whitespace collapsed; a standalone "x" word
            // becomes the hybrid sign again. Hyphens, periods and rank markers (subsp., var.) are kept.
            migrationBuilder.Sql($"""
                CREATE FUNCTION public.plant_scientific_identity(value text) RETURNS text
                LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
                RETURN pg_catalog.regexp_replace(
                    pg_catalog.btrim(pg_catalog.regexp_replace(
                        pg_catalog.lower(public.unaccent('public.unaccent'::regdictionary,
                            pg_catalog.replace(pg_catalog.normalize(value, 'NFC'), '{HybridSign}', ' x ')) COLLATE "und-x-icu"),
                        '{Whitespace}', ' ', 'g')),
                    '(^| )x( |$)', '\1{HybridSign}\2', 'g');
                """);

            // Cultivar name: NULL or blank becomes '' (not STRICT: NULLs never collide in a unique index,
            // which would allow duplicate species-level plants); NFC; whitespace collapsed; one pair of
            // wrapping quotes removed ('Peace' = Peace); ICU lower-case. Accents and inner punctuation are
            // kept, because cultivar names are written as published (Ophélia, Mr. Lincoln, Beni-kaname).
            // COLLATE is applied to lower()'s argument, so the ICU rules decide the case folding.
            migrationBuilder.Sql($"""
                CREATE FUNCTION public.plant_cultivar_identity(value text) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE
                RETURN pg_catalog.lower(pg_catalog.btrim(pg_catalog.regexp_replace(
                    pg_catalog.btrim(pg_catalog.regexp_replace(
                        pg_catalog.normalize(coalesce(value, ''), 'NFC'), '{Whitespace}', ' ', 'g')),
                    '^[''‘’"“”](.*)[''‘’"“”]$', '\1')) COLLATE "und-x-icu");
                """);

            // Covers every status: a Draft duplicate is still a duplicate.
            migrationBuilder.Sql($"""
                CREATE UNIQUE INDEX {PlantIdentity.UniqueIndexName} ON plants (
                    public.plant_scientific_identity(scientific_name),
                    public.plant_cultivar_identity(cultivar_name));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DROP INDEX {PlantIdentity.UniqueIndexName};");
            migrationBuilder.Sql("DROP FUNCTION public.plant_cultivar_identity(text);");
            migrationBuilder.Sql("DROP FUNCTION public.plant_scientific_identity(text);");
        }
    }
}
