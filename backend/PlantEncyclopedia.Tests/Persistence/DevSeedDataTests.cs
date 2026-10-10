using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Tests.Persistence;

/// <summary>
/// Runs backend/dev-data/seed-dev-data.sql against the disposable test database only.
/// </summary>
[Collection(PostgresCollection.Name)]
public class DevSeedDataTests(PostgresDatabaseFixture fixture)
{
    private const string SampleDescription = "Development sample data — not verified.";

    private static readonly string SeedScript =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DevData", "seed-dev-data.sql"));

    // Protects against: the seed script drifting out of sync with the schema, or creating
    // duplicates when someone runs it twice.
    [Fact]
    public async Task SeedScript_Applies_AndRerunAddsNothing()
    {
        await RunSeedScriptAsync(fixture.ConnectionString);
        var first = await CountSeedRowsAsync();

        await RunSeedScriptAsync(fixture.ConnectionString);
        var second = await CountSeedRowsAsync();

        Assert.Equal(new SeedCounts(Categories: 6, PlantGroups: 12, Taxonomies: 13, Plants: 17, PlantNames: 12), first);
        Assert.Equal(first, second);
    }

    // Protects against: sample data that breaks the product rules the app will rely on.
    [Fact]
    public async Task SeedData_FollowsPublishingAndCultivarRules()
    {
        await RunSeedScriptAsync(fixture.ConnectionString);

        await using var db = fixture.CreateDbContext();
        var plants = await db.Plants.Where(p => p.Description == SampleDescription).ToListAsync();

        // 16 Published plants, each in a plant group; 1 Draft without a group.
        var published = plants.Where(p => p.Status == PlantStatus.Published).ToList();
        Assert.Equal(16, published.Count);
        Assert.All(published, p => Assert.NotNull(p.PlantGroupId));

        var draft = Assert.Single(plants, p => p.Status == PlantStatus.Draft);
        Assert.Null(draft.PlantGroupId);

        // The rose cultivars are distinct plants that share one taxonomy row.
        var roses = plants.Where(p => p.ScientificName == "Rosa × hybrida").ToList();
        Assert.Equal(4, roses.Count);
        Assert.Single(roses.Select(p => p.TaxonomyId).Distinct());
        Assert.Equal(4, roses.Select(p => p.CultivarName).Distinct().Count());
    }

    // Protects against: loading sample data into any database other than dev or test.
    [Fact]
    public async Task SeedScript_RefusesOtherDatabases()
    {
        // A throwaway database inside the disposable test container.
        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand("CREATE DATABASE seed_guard_check", admin);
            await create.ExecuteNonQueryAsync();
        }

        var other = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = "seed_guard_check" };

        var error = await Assert.ThrowsAsync<PostgresException>(() => RunSeedScriptAsync(other.ConnectionString));
        Assert.Contains("must not be loaded", error.MessageText);
    }

    private static async Task RunSeedScriptAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(SeedScript, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<SeedCounts> CountSeedRowsAsync()
    {
        await using var db = fixture.CreateDbContext();

        // Seed rows use fixed ID prefixes, so rows created by other tests are not counted.
        // Table names are constants from this file, never user input.
        async Task<int> Count(string table, string prefix)
        {
            var sql = $"SELECT count(*)::int AS \"Value\" FROM {table} WHERE id::text LIKE {{0}}";
            return await db.Database.SqlQueryRaw<int>(sql, prefix + "%").SingleAsync();
        }

        return new SeedCounts(
            await Count("categories", "d5c00000-"),
            await Count("plant_groups", "d5a00000-"),
            await Count("taxonomies", "d5b00000-"),
            await Count("plants", "d5e00000-"),
            await Count("plant_names", "d5f00000-"));
    }

    private sealed record SeedCounts(int Categories, int PlantGroups, int Taxonomies, int Plants, int PlantNames);
}
