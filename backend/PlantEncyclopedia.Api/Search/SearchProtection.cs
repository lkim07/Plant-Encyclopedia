using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;

namespace PlantEncyclopedia.Api.Search;

/// <summary>
/// Abuse protection for search (SECURITY §13): a per-client rate limit and a request timeout.
/// Both use ASP.NET Core's built-in middleware.
/// </summary>
public static class SearchProtection
{
    public const string RateLimitPolicy = "search";
    public const string TimeoutPolicy = "search";

    public const int RequestsPerMinute = 30;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public const string RateLimitedCode = "RATE_LIMITED";
    public const string RateLimitedMessage = "Too many requests. Please wait a moment and try again.";
    public const string TimeoutCode = "REQUEST_TIMEOUT";
    public const string TimeoutMessage = "The request took too long. Please try again.";

    public static IServiceCollection AddSearchProtection(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRateLimitedAsync;

            options.AddPolicy(RateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                ClientPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = RequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0 // reject immediately instead of holding requests open
                }));
        });

        // When the timeout fires, the request's cancellation token cancels the running database query.
        services.AddRequestTimeouts(options => options.AddPolicy(TimeoutPolicy, new RequestTimeoutPolicy
        {
            Timeout = Timeout,
            TimeoutStatusCode = StatusCodes.Status503ServiceUnavailable,
            WriteTimeoutResponse = context => context.Response.WriteAsJsonAsync(
                new { error = new { code = TimeoutCode, message = TimeoutMessage } })
        }));

        return services;
    }

    /// <summary>
    /// The client's IP address. IPv6 clients are grouped by their /64 network, because one
    /// subscriber usually controls a whole /64 and could otherwise rotate addresses to dodge the limit.
    /// Forwarded headers (X-Forwarded-For) are deliberately not trusted until a reverse proxy is
    /// configured with a known-proxy list; trusting them blindly would let clients fake their address.
    /// </summary>
    internal static string ClientPartitionKey(HttpContext httpContext)
    {
        var address = httpContext.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return new IPAddress(bytes) + "/64";
        }

        return address.ToString();
    }

    private static async ValueTask WriteRateLimitedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = new { code = RateLimitedCode, message = RateLimitedMessage } },
            cancellationToken);
    }
}
