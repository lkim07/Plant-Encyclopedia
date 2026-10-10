using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PlantEncyclopedia.Infrastructure.Persistence;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

[Collection(PostgresCollection.Name)]
public class HealthEndpointTests(PostgresDatabaseFixture fixture)
{
    // Protects against: /health reporting failure, or returning anything other than the documented body,
    // when the database is available.
    [Fact]
    public async Task Health_ReturnsHealthy_WhenDatabaseReachable()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        // The running API must be wired to the disposable test container, never the development database.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlantEncyclopediaDbContext>();
            var used = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            var expected = new NpgsqlConnectionStringBuilder(fixture.ConnectionString);

            Assert.Equal(PostgresDatabaseFixture.TestDatabaseName, used.Database);
            Assert.Equal(expected.Port, used.Port);
        }

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("""{"status":"healthy"}""", await response.Content.ReadAsStringAsync());
    }

    // Protects against: a database outage being reported as healthy, or leaking connection details.
    [Fact]
    public async Task Health_ReturnsUnhealthy_WhenDatabaseUnreachable()
    {
        // Port 1 has no PostgreSQL server, so the connection fails within the 2-second timeout.
        var passwordMarker = $"pw-{Guid.NewGuid():N}";
        var unreachable =
            $"Host=127.0.0.1;Port=1;Database={PostgresDatabaseFixture.TestDatabaseName};" +
            $"Username=test;Password={passwordMarker};Timeout=2";

        await using var factory = new TestApiFactory(unreachable);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("""{"status":"unhealthy"}""", body);

        // The failure is logged server-side, but the password never appears in the logs.
        Assert.Contains(factory.Logs.Entries, entry => entry.Contains("Unhealthy"));
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Contains(passwordMarker));
    }
}
