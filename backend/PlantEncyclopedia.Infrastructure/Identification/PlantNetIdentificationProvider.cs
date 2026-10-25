using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.Identification;

namespace PlantEncyclopedia.Infrastructure.Identification;

/// <summary>
/// Pl@ntNet single-species identification (POST /v2/identify/{project}, documented at my.plantnet.org/doc/api/identify):
/// one JPEG with organ "auto". HTTPS to my-api.plantnet.org only, no redirects, no retries, a 10-second timeout and a
/// 1 MB response limit. Any answer other than 200 with JSON is Unavailable: Pl@ntNet's documentation does not list
/// its error codes, so none is read as "not a plant". The API key travels in the query string, so request URLs are
/// never logged (the default HttpClient loggers are removed at registration).
/// </summary>
internal sealed partial class PlantNetIdentificationProvider(
    HttpClient http,
    PlantNetOptions options,
    ILogger<PlantNetIdentificationProvider> logger) : IPlantIdentificationProvider
{
    public static readonly Uri BaseAddress = new("https://my-api.plantnet.org/");
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    public const int MaxResponseBytes = 1024 * 1024;

    /// <summary>Suggestions requested (nb-results); only the likely ones are used.</summary>
    public const int ResultsRequested = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void ConfigureClient(HttpClient client)
    {
        client.BaseAddress = BaseAddress;
        client.Timeout = Timeout;
        client.MaxResponseContentBufferSize = MaxResponseBytes;

        var version = typeof(PlantNetIdentificationProvider).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PlantEncyclopedia", version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ProviderIdentification> IdentifyAsync(byte[] jpeg, CancellationToken cancellationToken)
    {
        var requestUri =
            $"v2/identify/{Uri.EscapeDataString(options.Project)}" +
            $"?api-key={Uri.EscapeDataString(options.ApiKey!)}&nb-results={ResultsRequested}";

        using var image = new ByteArrayContent(jpeg);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        using var form = new MultipartFormDataContent
        {
            { image, "images", "photo.jpg" },
            { new StringContent("auto"), "organs" }
        };

        try
        {
            // The default completion option buffers the body, enforcing MaxResponseContentBufferSize.
            using var response = await http.PostAsync(requestUri, form, cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                LogUnexpectedStatus(logger, (int)response.StatusCode);
                return ProviderIdentification.Unavailable;
            }

            if (response.Content.Headers.ContentType?.MediaType != "application/json")
            {
                LogUnexpectedContentType(logger);
                return ProviderIdentification.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<PlantNetIdentifyResponse>(JsonOptions, cancellationToken);
            return ProviderIdentification.Answered(ToCandidates(body));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimedOut(logger, Timeout.TotalSeconds);
            return ProviderIdentification.Unavailable;
        }
        catch (HttpRequestException e)
        {
            // Not logged as an exception: its message could include the request URL, which holds the key.
            LogRequestFailed(logger, e.HttpRequestError);
            return ProviderIdentification.Unavailable;
        }
        catch (JsonException)
        {
            LogInvalidJson(logger);
            return ProviderIdentification.Unavailable;
        }
    }

    // Results without a usable name or score are skipped; the service normalizes and checks the names.
    private static List<ProviderCandidate> ToCandidates(PlantNetIdentifyResponse? body) =>
        (body?.Results ?? [])
            .Where(r => r?.Species?.ScientificNameWithoutAuthor is { Length: > 0 } && r.Score is >= 0 and <= 1)
            .Select(r => new ProviderCandidate(r!.Species!.ScientificNameWithoutAuthor!, r.Score!.Value))
            .ToList();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pl@ntNet identification returned HTTP {StatusCode}.")]
    private static partial void LogUnexpectedStatus(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pl@ntNet identification returned a response that is not JSON.")]
    private static partial void LogUnexpectedContentType(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pl@ntNet identification timed out after {Seconds} seconds.")]
    private static partial void LogTimedOut(ILogger logger, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pl@ntNet identification request failed ({Error}).")]
    private static partial void LogRequestFailed(ILogger logger, HttpRequestError error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pl@ntNet identification returned invalid JSON.")]
    private static partial void LogInvalidJson(ILogger logger);
}

/// <summary>The fields used from Pl@ntNet's identify response (field names as documented).</summary>
internal sealed record PlantNetIdentifyResponse(IReadOnlyList<PlantNetResult?>? Results);

internal sealed record PlantNetResult(double? Score, PlantNetSpecies? Species);

internal sealed record PlantNetSpecies(string? ScientificNameWithoutAuthor);
