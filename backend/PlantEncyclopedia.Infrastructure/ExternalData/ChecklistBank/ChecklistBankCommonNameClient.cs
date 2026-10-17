using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;

namespace PlantEncyclopedia.Infrastructure.ExternalData.ChecklistBank;

/// <summary>
/// Fetches common names for a Catalogue of Life XR taxon from ChecklistBank (ARCHITECTURE §21). The taxon IDs
/// GBIF returns for COL XR are COL IDs, so they are looked up in dataset 3LXR (the latest COL XR release).
/// One GET per import: HTTPS to api.checklistbank.org only, no redirects, no retries, a 1.5-second timeout
/// (GBIF's 3 seconds plus this stay under the lookup endpoint's 5) and a 1 MB response limit. Names are
/// optional, so every failure returns no names and the plant is imported under its scientific name.
/// </summary>
internal sealed partial class ChecklistBankCommonNameClient(
    HttpClient http,
    ILogger<ChecklistBankCommonNameClient> logger)
{
    public static readonly Uri BaseAddress = new("https://api.checklistbank.org/");
    public const string DatasetKey = "3LXR";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1.5);
    public const int MaxResponseBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Same User-Agent and limits style as the GBIF client.</summary>
    public static void ConfigureClient(HttpClient client, GbifOptions options)
    {
        client.BaseAddress = BaseAddress;
        client.Timeout = Timeout;
        client.MaxResponseContentBufferSize = MaxResponseBytes;

        var version = typeof(ChecklistBankCommonNameClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PlantEncyclopedia", version));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"(+{options.Contact})"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IReadOnlyList<ExternalCommonName>> GetAsync(string taxonId, CancellationToken cancellationToken)
    {
        // ExternalTaxonPolicy allows dots in IDs; "." or ".." would be read as a path segment, not an ID.
        if (taxonId.All(c => c == '.')) return [];

        var requestUri = $"dataset/{DatasetKey}/taxon/{Uri.EscapeDataString(taxonId)}/vernacular";

        try
        {
            using var response = await http.GetAsync(requestUri, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                LogTaxonNotFound(logger);
                return [];
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                LogUnexpectedStatus(logger, (int)response.StatusCode);
                return [];
            }

            if (response.Content.Headers.ContentType?.MediaType != "application/json")
            {
                LogUnexpectedContentType(logger);
                return [];
            }

            var body = await response.Content.ReadFromJsonAsync<List<ChecklistBankVernacularName?>>(JsonOptions, cancellationToken);
            return (body ?? [])
                .Where(n => !string.IsNullOrWhiteSpace(n?.Name))
                .Select(n => new ExternalCommonName(n!.Name!, n.Language, HasReference(n.ReferenceId)))
                .ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimedOut(logger, Timeout.TotalSeconds);
            return [];
        }
        catch (HttpRequestException e)
        {
            // Not logged as an exception: its message could include response content.
            LogRequestFailed(logger, e.HttpRequestError);
            return [];
        }
        catch (JsonException)
        {
            LogInvalidJson(logger);
            return [];
        }
    }

    private static bool HasReference(JsonElement? reference) => reference switch
    {
        { ValueKind: JsonValueKind.String } r => !string.IsNullOrWhiteSpace(r.GetString()),
        { ValueKind: JsonValueKind.Number } => true,
        _ => false
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "ChecklistBank has no COL XR taxon for this ID; no common names imported.")]
    private static partial void LogTaxonNotFound(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ChecklistBank common names returned HTTP {StatusCode}.")]
    private static partial void LogUnexpectedStatus(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ChecklistBank common names returned a non-JSON response.")]
    private static partial void LogUnexpectedContentType(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ChecklistBank common names timed out after {Seconds} s.")]
    private static partial void LogTimedOut(ILogger logger, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ChecklistBank common names request failed ({Error}).")]
    private static partial void LogRequestFailed(ILogger logger, HttpRequestError error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ChecklistBank common names returned invalid JSON.")]
    private static partial void LogInvalidJson(ILogger logger);
}
