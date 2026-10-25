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
/// reasonably likely suggestions in its order, and match them against Published plants. Only matched plants are
/// returned (D-011), at most <see cref="MaxCandidates"/>; a suggestion never creates a plant. Nothing is stored.
/// </summary>
public sealed class PlantIdentificationService(
    IIdentificationImageProcessor images,
    IPlantIdentificationProvider provider,
    IIdentificationQueries queries)
{
    /// <summary>Candidates returned (D-012: #1, #2, #3; More is separate).</summary>
    public const int MaxCandidates = 3;

    /// <summary>Suggestions below this provider score are ignored. A tuning value, not a confidence shown to users.</summary>
    public const double MinScore = 0.05;

    /// <summary>Suggestions looked up in the database.</summary>
    public const int MaxNamesLookedUp = 10;

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

        var plants = (await queries.FindPublishedAsync(names, cancellationToken))
            .OrderBy(p => p.CandidateIndex)
            .DistinctBy(p => p.Id)
            .Take(MaxCandidates)
            .ToList();
        return plants.Count == 0
            ? IdentificationResult.Of(IdentificationStatus.NotIdentified)
            : new IdentificationResult(IdentificationStatus.Identified, plants);
    }
}
