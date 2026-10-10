using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Api.Errors;

namespace PlantEncyclopedia.Tests.Api;

public class GlobalExceptionHandlerTests
{
    // Protects against: exception messages, stack traces, database errors or file paths
    // reaching the client.
    [Fact]
    public async Task UnhandledException_ReturnsSafeErrorResponse()
    {
        var logs = new CapturingLoggerProvider();
        using var loggerFactory = new LoggerFactory([logs]);
        var handler = new GlobalExceptionHandler(loggerFactory.CreateLogger<GlobalExceptionHandler>());

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/plants/123";
        context.Response.Body = new MemoryStream();

        var exception = new InvalidOperationException(
            @"Npgsql.PostgresException: relation ""plants"" failed at C:\Projects\PlantEncyclopedia\Secret.cs");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(
            """{"error":{"code":"INTERNAL_SERVER_ERROR","message":"Something went wrong. Please try again."}}""",
            body);
        Assert.DoesNotContain("Npgsql", body);
        Assert.DoesNotContain(@"C:\", body);
        Assert.DoesNotContain("InvalidOperationException", body);

        // The details are kept for diagnosis in the server-side log only.
        // Note: the handler logs the exception as-is, so code must never put secrets in exception messages.
        Assert.Contains(logs.Entries, entry => entry.Contains("InvalidOperationException"));
    }
}
