using Microsoft.EntityFrameworkCore;

namespace PlantEncyclopedia.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class MigrationTests(PostgresDatabaseFixture fixture)
{
    // Protects against: tests accidentally running against the development database.
    [Fact]
    public async Task Tests_RunAgainstDisposableTestDatabase()
    {
        await using var db = fixture.CreateDbContext();

        var database = await db.Database
            .SqlQueryRaw<string>("SELECT current_database() AS \"Value\"")
            .SingleAsync();

        Assert.Equal(PostgresDatabaseFixture.TestDatabaseName, database);
    }

    // Protects against: a migration that fails on an empty database.
    [Fact]
    public async Task Migrations_AreAllApplied()
    {
        await using var db = fixture.CreateDbContext();

        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_InitialCreate"));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    // Protects against: changing entities or configurations without adding a migration.
    [Fact]
    public void Model_HasNoPendingChanges()
    {
        using var db = fixture.CreateDbContext();

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
