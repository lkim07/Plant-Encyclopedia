using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Infrastructure.Persistence;

namespace PlantEncyclopedia.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class CheckConstraintTests(PostgresDatabaseFixture fixture)
{
    // Protects against: text outside PlantNameType being written directly with SQL.
    [Fact]
    public async Task InvalidNameType_IsRejectedByCheckConstraint()
    {
        await using var db = fixture.CreateDbContext();
        var plantId = await CreatePlantAsync(db);

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO plant_names (id, plant_id, name, name_type, language_code)
            VALUES ({Guid.NewGuid()}, {plantId}, {"Test name"}, {"Nickname"}, {"en"})
            """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_plant_names_name_type", error.ConstraintName);
    }

    // Protects against: text outside PlantSourceType being written directly with SQL.
    [Fact]
    public async Task InvalidSourceType_IsRejectedByCheckConstraint()
    {
        await using var db = fixture.CreateDbContext();
        var plantId = await CreatePlantAsync(db);

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO plant_sources (id, plant_id, provider, external_id, source_type, retrieved_at)
            VALUES ({Guid.NewGuid()}, {plantId}, {"Test"}, {"1"}, {"Rumor"}, {DateTimeOffset.UtcNow})
            """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_plant_sources_source_type", error.ConstraintName);
    }

    // Protects against: an out-of-range enum cast in C#. EF stores (PlantNameType)99 as the text "99",
    // which the database rejects.
    [Fact]
    public async Task OutOfRangeEnumCast_IsRejectedByCheckConstraint()
    {
        await using var db = fixture.CreateDbContext();

        db.PlantNames.Add(new PlantName
        {
            Plant = TestData.NewPlant(),
            Name = "Test name",
            NameType = (PlantNameType)99,
            LanguageCode = "en"
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgresError = Assert.IsType<PostgresException>(error.InnerException);

        Assert.Equal(PostgresErrorCodes.CheckViolation, postgresError.SqlState);
        Assert.Equal("ck_plant_names_name_type", postgresError.ConstraintName);
    }

    private static async Task<Guid> CreatePlantAsync(PlantEncyclopediaDbContext db)
    {
        var plant = TestData.NewPlant();
        db.Plants.Add(plant);
        await db.SaveChangesAsync();
        return plant.Id;
    }
}
