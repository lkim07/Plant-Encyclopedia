using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class PlantNamePersistenceTests(PostgresDatabaseFixture fixture)
{
    // Protects against: NameType being saved or read back incorrectly (enum <-> text conversion).
    [Fact]
    public async Task PlantName_NameType_Synonym_RoundTrips()
    {
        Guid nameId;

        await using (var db = fixture.CreateDbContext())
        {
            var name = new PlantName
            {
                Plant = TestData.NewPlant(),
                Name = "Rosa odorata",
                NameType = PlantNameType.Synonym
            };

            db.PlantNames.Add(name);
            await db.SaveChangesAsync();
            nameId = name.Id;
        }

        // A new DbContext has nothing cached, so these values come from PostgreSQL.
        await using (var db = fixture.CreateDbContext())
        {
            var loaded = await db.PlantNames.SingleAsync(n => n.Id == nameId);
            Assert.Equal(PlantNameType.Synonym, loaded.NameType);

            var storedText = await db.Database
                .SqlQuery<string>($"SELECT name_type AS \"Value\" FROM plant_names WHERE id = {nameId}")
                .SingleAsync();
            Assert.Equal("Synonym", storedText);
        }
    }
}
