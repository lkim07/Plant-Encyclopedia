using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class RequiredMemberLimitationTests(PostgresDatabaseFixture fixture)
{
    // KNOWN LIMITATION (documented, not fixed):
    // `required` only forces callers to assign RetrievedAt. It does not stop them from assigning
    // `default` (0001-01-01). That value passes the NOT NULL column and is saved without error.
    // Runtime validation is intentionally deferred; if it is added later, update this test.
    [Fact]
    public async Task Required_DoesNotPreventExplicitDefaultRetrievedAt()
    {
        Guid sourceId;

        await using (var db = fixture.CreateDbContext())
        {
            var source = new PlantSource
            {
                Plant = TestData.NewPlant(),
                Provider = "Test",
                ExternalId = Guid.NewGuid().ToString("N"),
                SourceType = PlantSourceType.Taxonomy,
                RetrievedAt = default // compiles, because the property was assigned
            };

            db.PlantSources.Add(source);
            await db.SaveChangesAsync(); // no exception: the database accepts it
            sourceId = source.Id;
        }

        await using (var db = fixture.CreateDbContext())
        {
            var loaded = await db.PlantSources.SingleAsync(s => s.Id == sourceId);
            Assert.Equal(default, loaded.RetrievedAt);
        }
    }
}
