using PlantEncyclopedia.Application.ExternalData;

namespace PlantEncyclopedia.Application.Identification;

public enum IdentificationStatus
{
    /// <summary>At least one suggestion matched a Published plant.</summary>
    Identified,

    /// <summary>The upload is not an acceptable image.</summary>
    InvalidImage,

    /// <summary>The provider answered, but nothing it suggested is in the encyclopedia (or it suggested nothing).</summary>
    NotIdentified,

    /// <summary>The provider could not be asked or did not answer.</summary>
    Unavailable
}

public sealed record IdentificationResult(IdentificationStatus Status, IReadOnlyList<MatchedPlant> Candidates)
{
    public static IdentificationResult Of(IdentificationStatus status) => new(status, []);
}

/// <summary>
/// Plant identification (PRD §8–9, ARCHITECTURE §25–28): check and re-encode the photo, ask the provider, keep its
/// reasonably likely suggestions in its order, and match them against Published plants. A suggestion missing from
/// the encyclopedia is imported through Catalogue of Life (D-072; at most <see cref="MaxImports"/> per photo), so the
/// plant data always comes from Catalogue of Life, never from the identification provider (D-011). Only Published
/// plants are returned, at most <see cref="MaxCandidates"/>, in the provider's order. The photo is not stored.
/// </summary>
public sealed class PlantIdentificationService(
    IIdentificationImageProcessor images,
    IPlantIdentificationProvider provider,
    IIdentificationQueries queries,
    ISuggestionImporter importer)
{
    /// <summary>Candidates returned (D-012: #1, #2, #3; More is separate).</summary>
    public const int MaxCandidates = 3;

    /// <summary>Suggestions below this provider score are ignored. A tuning value, not a confidence shown to users.</summary>
    public const double MinScore = 0.05;

    /// <summary>Suggestions looked up in the database.</summary>
    public const int MaxNamesLookedUp = 10;

    /// <summary>Catalogue of Life imports tried per photo (each may take a few seconds and uses the GBIF budget).</summary>
    public const int MaxImports = 3;

    public async Task<IdentificationResult> IdentifyAsync(byte[] upload, CancellationToken cancellationToken)
    {
        var jpeg = images.Prepare(upload);
        if (jpeg is null)
        {
            return IdentificationResult.Of(IdentificationStatus.InvalidImage);
        }

        var answer = await provider.IdentifyAsync(jpeg, cancellationToken);
        if (answer.Status == ProviderIdentificationStatus.Unavailable)
        {
            return IdentificationResult.Of(IdentificationStatus.Unavailable);
        }

        // Provider text is untrusted: names are normalized and length-checked like any requested name.
        var names = answer.Candidates
            .Where(c => c.Score >= MinScore)
            .OrderByDescending(c => c.Score)
            .Select(c => ExternalTaxonPolicy.NormalizeRequestedName(c.ScientificName))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(MaxNamesLookedUp)
            .ToList();
        if (names.Count == 0)
        {
            return IdentificationResult.Of(IdentificationStatus.NotIdentified);
        }

        var known = (await queries.FindPublishedAsync(names, cancellationToken)).ToLookup(p => p.CandidateIndex);
        var plants = new List<MatchedPlant>();
        var imports = 0;
        for (var index = 0; index < names.Count && plants.Count < MaxCandidates; index++)
        {
            var forName = known[index].ToList();
            if (forName.Count == 0 && imports < MaxImports)
            {
                imports++;
                if (await importer.ImportAsync(names[index], cancellationToken) is { } plantId
                    && await queries.FindPublishedByIdAsync(plantId, cancellationToken) is { } imported)
                {
                    forName = [imported with { CandidateIndex = index }];
                }
            }

            plants.AddRange(forName.Where(p => plants.All(existing => existing.Id != p.Id)));
        }

        plants = plants.Take(MaxCandidates).ToList();
        return plants.Count == 0
            ? IdentificationResult.Of(IdentificationStatus.NotIdentified)
            : new IdentificationResult(IdentificationStatus.Identified, plants);
    }
}
