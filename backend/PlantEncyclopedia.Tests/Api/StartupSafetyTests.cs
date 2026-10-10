namespace PlantEncyclopedia.Tests.Api;

public class StartupSafetyTests
{
    // Protects against: the API (or the tests) silently falling back to another database
    // when no connection string is supplied. Outside Development, User Secrets are not loaded,
    // so startup must fail instead.
    [Fact]
    public async Task Startup_Fails_WhenNoConnectionStringIsConfigured()
    {
        await using var factory = new TestApiFactory(connectionString: null);

        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains(
            ExceptionChain(error),
            e => e is InvalidOperationException && e.Message.Contains("is not configured"));
    }

    private static IEnumerable<Exception> ExceptionChain(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
