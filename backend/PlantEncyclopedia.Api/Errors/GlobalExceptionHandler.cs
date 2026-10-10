using Microsoft.AspNetCore.Diagnostics;

namespace PlantEncyclopedia.Api.Errors;

/// <summary>
/// Turns any unhandled exception into the documented 500 error response (API_SPEC §12).
/// The client never sees exception messages, stack traces or database details;
/// the exception is logged server-side instead.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const string ErrorCode = "INTERNAL_SERVER_ERROR";
    public const string ErrorMessage = "Something went wrong. Please try again.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Path only: the query string could carry user data or tokens.
        logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new { error = new { code = ErrorCode, message = ErrorMessage } },
            cancellationToken);

        return true;
    }
}
