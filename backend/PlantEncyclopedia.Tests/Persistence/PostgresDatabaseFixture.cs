using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PlantEncyclopedia.Infrastructure;
using PlantEncyclopedia.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PlantEncyclopedia.Tests.Persistence;

/// <summary>
/// Starts one disposable PostgreSQL 16 container for the whole test run, applies the real
/// EF Core migrations to it, and hands out fresh DbContexts. The container is deleted afterwards.
/// The connection string comes only from the container; .env and User Secrets are never read.
/// </summary>
public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    public const string TestDatabaseName = "plant_encyclopedia_test";

    private const string DevelopmentDatabaseName = "plant_encyclopedia_dev";
    private const int DevelopmentPort = 5433;

    // Random password and a dynamically assigned host port for every run.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase(TestDatabaseName)
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    private ServiceProvider? _services;
    private IServiceScope? _scope;
    private DbContextOptions<PlantEncyclopediaDbContext>? _options;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();
        EnsureDisposableTestDatabase(ConnectionString);

        // Same registration as the API, so naming conventions and configurations match production.
        _services = new ServiceCollection()
            .AddInfrastructure(ConnectionString)
            .BuildServiceProvider();

        // The options keep a reference to this scope, so it lives as long as the fixture.
        _scope = _services.CreateScope();
        _options = _scope.ServiceProvider.GetRequiredService<DbContextOptions<PlantEncyclopediaDbContext>>();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>Creates a new DbContext with an empty change tracker.</summary>
    public PlantEncyclopediaDbContext CreateDbContext() =>
        new(_options ?? throw new InvalidOperationException("Fixture is not initialized."));

    public async Task DisposeAsync()
    {
        _scope?.Dispose();

        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    // Fail fast before any database command if the connection could reach the development database.
    private static void EnsureDisposableTestDatabase(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (string.Equals(builder.Database, DevelopmentDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to run tests against '{DevelopmentDatabaseName}'.");
        }

        if (builder.Port == DevelopmentPort)
        {
            throw new InvalidOperationException($"Refusing to run tests on development port {DevelopmentPort}.");
        }

        if (builder.Database != TestDatabaseName)
        {
            throw new InvalidOperationException($"Expected test database '{TestDatabaseName}'.");
        }
    }
}

/// <summary>
/// All database tests share one container. Tests in the same collection run one at a time,
/// and each test also creates its own uniquely named data.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresDatabaseFixture>
{
    public const string Name = "PostgreSQL";
}
