using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace PlantEncyclopedia.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class SearchKeyFunctionTests(PostgresDatabaseFixture fixture)
{
    // Protects against: search missing names because of case, accents or punctuation, or
    // LIKE wildcards (% _ \) surviving into search patterns.
    [Theory]
    [InlineData("Café 'Mr. Lincoln'", "cafe mr lincoln")]
    [InlineData("  ÉCHEVERIA  ", "echeveria")]
    [InlineData("Rosa × hybrida", "rosa hybrida")]
    [InlineData("Œillet", "oeillet")]
    [InlineData(@"a%b_c\d", "a b c d")]
    [InlineData("장미 (Rose)", "장미 rose")]
    [InlineData("black—eyed…susan", "black eyed susan")]
    [InlineData("'; DROP TABLE plants; --", "drop table plants")]
    [InlineData("!?…", "")]
    public async Task SearchKey_FoldsCaseAccentsAndPunctuation(string input, string expected)
    {
        await using var db = fixture.CreateDbContext();

        var key = await db.Database
            .SqlQuery<string>($"SELECT public.search_key({input}) AS \"Value\"")
            .SingleAsync();

        Assert.Equal(expected, key);
    }

    // Protects against: the function or a search index going missing from the migration.
    [Fact]
    public async Task SearchIndexes_Exist()
    {
        await using var db = fixture.CreateDbContext();

        var indexes = await db.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE indexname LIKE '%_search' ORDER BY indexname")
            .ToListAsync();

        Assert.Equal(
            [
                "ix_plant_groups_name_search",
                "ix_plant_names_name_search",
                "ix_plants_cultivar_name_search",
                "ix_plants_display_name_search",
                "ix_plants_scientific_name_search"
            ],
            indexes);
    }

    // Protects against: the indexes existing but never matching the query expression (silent full scans).
    [Fact]
    public async Task ContainsSearch_CanUseTrigramIndex()
    {
        await using var db = fixture.CreateDbContext();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // The test table is tiny, so forbid sequential scans to see whether the index is usable at all.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off");

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "EXPLAIN SELECT id FROM plants WHERE public.search_key(display_name) LIKE '%rose%'";
        var plan = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                plan.Add(reader.GetString(0));
            }
        }

        Assert.Contains(plan, line => line.Contains("ix_plants_display_name_search"));
    }
}
