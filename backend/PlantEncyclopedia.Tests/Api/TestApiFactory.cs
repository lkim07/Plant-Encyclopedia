using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// Runs the real API in memory against a connection string supplied by the test.
/// </summary>
internal sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string? _connectionString;

    /// <param name="connectionString">
    /// The connection string the API must use. Pass null to start the API with none at all.
    /// </param>
    public TestApiFactory(string? connectionString)
    {
        if (connectionString is not null)
        {
            PostgresDatabaseFixture.EnsureDisposableTestDatabase(connectionString);
        }

        _connectionString = connectionString;
    }

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": User Secrets, which hold the development connection string, are never loaded.
        builder.UseEnvironment("Testing");

        if (_connectionString is not null)
        {
            builder.UseSetting("ConnectionStrings:PlantEncyclopedia", _connectionString);
        }

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
    }
}
