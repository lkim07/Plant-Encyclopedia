using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData;

namespace PlantEncyclopedia.Infrastructure.ExternalData.Gbif;

/// <summary>
/// Matches scientific names against Catalogue of Life XR through the GBIF v2 species match API
/// (ARCHITECTURE §21). One GET per lookup: HTTPS to api.gbif.org only, no redirects, no retries,
/// a 3-second timeout and a 1 MB response limit. Every failure becomes Unavailable. The requested
/// name is never logged: the default HttpClient request logging (which logs the URL) is removed.
/// </summary>
internal sealed partial class GbifPlantDataProvider(
    HttpClient http,
    GbifOptions options,
    ILogger<GbifPlantDataProvider> logger) : IPlantDataProvider
{
    /// <summary>Stored in plant_sources.provider.</summary>
    public const string ProviderName = KnownProviders.GbifCatalogueOfLife;

    public static readonly Uri BaseAddress = new("https://api.gbif.org/");
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    public const int MaxResponseBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void ConfigureClient(HttpClient client, GbifOptions options)
    {
        client.BaseAddress = BaseAddress;
        client.Timeout = Timeout;
        client.MaxResponseContentBufferSize = MaxResponseBytes;

        var version = typeof(GbifPlantDataProvider).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PlantEncyclopedia", version));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"(+{options.Contact})"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<TaxonLookupResult> MatchAsync(string scientificName, CancellationToken cancellationToken)
    {
        // kingdom=Plantae is a hint, not a filter: an animal name can still come back, so the
        // classification is checked by ExternalTaxonPolicy.
        var requestUri =
            $"v2/species/match?scientificName={Uri.EscapeDataString(scientificName)}" +
            $"&checklistKey={Uri.EscapeDataString(options.ChecklistKey!)}&kingdom={ExternalTaxonPolicy.PlantKingdom}";

        try
        {
            // The default completion option buffers the body, enforcing MaxResponseContentBufferSize.
            using var response = await http.GetAsync(requestUri, cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                LogUnexpectedStatus(logger, (int)response.StatusCode);
                return TaxonLookupResult.Unavailable;
            }

            if (response.Content.Headers.ContentType?.MediaType != "application/json")
            {
                LogUnexpectedContentType(logger);
                return TaxonLookupResult.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<GbifMatchResponse>(JsonOptions, cancellationToken);
            return ToResult(body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimedOut(logger, Timeout.TotalSeconds);
            return TaxonLookupResult.Unavailable;
        }
        catch (HttpRequestException e)
        {
            // Not logged as an exception: its message could include response content.
            LogRequestFailed(logger, e.HttpRequestError);
            return TaxonLookupResult.Unavailable;
        }
        catch (JsonException)
        {
            LogInvalidJson(logger);
            return TaxonLookupResult.Unavailable;
        }
    }

    /// <summary>
    /// None: GBIF's v2 taxon details endpoint, which would have them, returned 404. The guarded provider
    /// fetches them from ChecklistBank instead (<see cref="GuardedPlantDataProvider"/>).
    /// </summary>
    public Task<IReadOnlyList<ExternalCommonName>> GetCommonNamesAsync(ExternalTaxon taxon, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExternalCommonName>>([]);

    private TaxonLookupResult ToResult(GbifMatchResponse? body)
    {
        var usage = body?.Usage;
        if (body is null || usage is null || string.IsNullOrWhiteSpace(usage.Key)
            || body.Diagnostics?.MatchType is null or "NONE")
        {
            return TaxonLookupResult.NotFound;
        }

        var scientificName = WithoutAuthorship(usage.Name, usage.Authorship);
        if (scientificName is null)
        {
            LogUnusableName(logger);
            return TaxonLookupResult.NotFound;
        }

        var ranks = body.Classification ?? [];
        string? NameAtRank(string rank) => ranks.FirstOrDefault(c => c.Rank == rank)?.Name;

        return TaxonLookupResult.Found(new ExternalTaxon(
            Provider: ProviderName,
            ExternalId: usage.Key,
            ScientificName: scientificName,
            Rank: usage.Rank switch
            {
                "SPECIES" => TaxonRank.Species,
                "SUBSPECIES" => TaxonRank.Subspecies,
                "VARIETY" => TaxonRank.Variety,
                _ => TaxonRank.Other
            },
            IsExactMatch: body.Diagnostics.MatchType == "EXACT",
            IsAccepted: usage.Status == "ACCEPTED" && body.Synonym != true,
            Classification: new TaxonClassification(
                Kingdom: NameAtRank("KINGDOM"),
                Phylum: NameAtRank("PHYLUM"),
                Class: NameAtRank("CLASS"),
                Order: NameAtRank("ORDER"),
                Family: NameAtRank("FAMILY"),
                Genus: NameAtRank("GENUS"),
                Species: NameAtRank("SPECIES"))));
    }

    /// <summary>
    /// "Mentha × piperita L." with authorship "L." becomes "Mentha × piperita". canonicalName is not used
    /// because it drops the hybrid sign and rank markers. If the authorship is not a suffix of the name,
    /// the name is not trusted (null).
    /// </summary>
    internal static string? WithoutAuthorship(string? name, string? authorship)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (string.IsNullOrWhiteSpace(authorship)) return name;

        var suffix = " " + authorship;
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "GBIF match returned HTTP {StatusCode}.")]
    private static partial void LogUnexpectedStatus(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GBIF match returned a non-JSON response.")]
    private static partial void LogUnexpectedContentType(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GBIF match timed out after {Seconds} s.")]
    private static partial void LogTimedOut(ILogger logger, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GBIF match request failed ({Error}).")]
    private static partial void LogRequestFailed(ILogger logger, HttpRequestError error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GBIF match returned invalid JSON.")]
    private static partial void LogInvalidJson(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GBIF match returned a name that does not end with its authorship.")]
    private static partial void LogUnusableName(ILogger logger);
}
