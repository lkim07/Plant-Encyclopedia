using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class PlantSourcePersistenceTests(PostgresDatabaseFixture fixture)
{
    // Protects against: SourceType being saved or read back incorrectly (enum <-> text conversion).
    [Fact]
    public async Task PlantSource_SourceType_Care_RoundTrips()
    {
        Guid sourceId;

        await using (var db = fixture.CreateDbContext())
        {
            // A curated source identified by URL only (no external ID).
            var source = new PlantSource
            {
                Plant = TestData.NewPlant(),
                Provider = "Curated",
                SourceUrl = $"https://example.org/care/{Guid.NewGuid():N}",
                SourceType = PlantSourceType.Care,
                RetrievedAt = DateTimeOffset.UtcNow
            };

            db.PlantSources.Add(source);
            await db.SaveChangesAsync();
            sourceId = source.Id;
        }

        // A new DbContext has nothing cached, so these values come from PostgreSQL.
        await using (var db = fixture.CreateDbContext())
        {
            var loaded = await db.PlantSources.SingleAsync(s => s.Id == sourceId);
            Assert.Equal(PlantSourceType.Care, loaded.SourceType);

            var storedText = await db.Database
                .SqlQuery<string>($"SELECT source_type AS \"Value\" FROM plant_sources WHERE id = {sourceId}")
                .SingleAsync();
            Assert.Equal("Care", storedText);
        }
    }
}
