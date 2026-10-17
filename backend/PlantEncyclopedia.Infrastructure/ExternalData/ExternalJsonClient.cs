using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;

namespace PlantEncyclopedia.Infrastructure.ExternalData;

/// <summary>
/// One GET returning JSON from a fixed external host, for the image sources. Same rules as the GBIF client:
/// no redirects (set by the handler registration), no retries, a timeout and a 1 MB limit, JSON only.
/// Every failure is logged without the URL (it can contain the plant name) and returns null.
/// </summary>
internal static partial class ExternalJsonClient
{
    public const int MaxResponseBytes = 1024 * 1024;

    public static void Configure(HttpClient client, Uri? baseAddress, TimeSpan timeout, GbifOptions options)
    {
        client.BaseAddress = baseAddress;
        client.Timeout = timeout;
        client.MaxResponseContentBufferSize = MaxResponseBytes;

        var version = typeof(ExternalJsonClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PlantEncyclopedia", version));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"(+{options.Contact})"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public static async Task<T?> GetAsync<T>(
        HttpClient http,
        string requestUri,
        JsonSerializerOptions jsonOptions,
        string source,
        ILogger logger,
        CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var response = await http.GetAsync(requestUri, cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                LogUnexpectedStatus(logger, source, (int)response.StatusCode);
                return null;
            }

            if (response.Content.Headers.ContentType?.MediaType != "application/json")
            {
                LogUnexpectedContentType(logger, source);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<T>(jsonOptions, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimedOut(logger, source, http.Timeout.TotalSeconds);
            return null;
        }
        catch (HttpRequestException e)
        {
            // Not logged as an exception: its message could include response content.
            LogRequestFailed(logger, source, e.HttpRequestError);
            return null;
        }
        catch (JsonException)
        {
            LogInvalidJson(logger, source);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Source} returned HTTP {StatusCode}.")]
    private static partial void LogUnexpectedStatus(ILogger logger, string source, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Source} returned a non-JSON response.")]
    private static partial void LogUnexpectedContentType(ILogger logger, string source);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Source} timed out after {Seconds} s.")]
    private static partial void LogTimedOut(ILogger logger, string source, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Source} request failed ({Error}).")]
    private static partial void LogRequestFailed(ILogger logger, string source, HttpRequestError error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Source} returned invalid JSON.")]
    private static partial void LogInvalidJson(ILogger logger, string source);
}
