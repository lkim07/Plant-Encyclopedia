using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Application.ExternalData.Images;

/// <param name="Photo">The chosen photo, or null when no source offers an acceptable one.</param>
/// <param name="Illustration">The chosen botanical illustration, or null.</param>
public sealed record SelectedPlantImages(AcceptedImage? Photo, AcceptedImage? Illustration);

/// <summary>
/// Picks one photo and one illustration for a plant automatically (ARCHITECTURE §22). Sources of each kind
/// are asked in registration order (photos: iNaturalist, then Wikipedia), and the first candidate that
/// <see cref="ExternalImagePolicy"/> accepts wins. Later sources are not asked once a kind is settled.
/// </summary>
public sealed class PlantImageSelector(IEnumerable<IPlantImageSource> sources)
{
    private readonly IReadOnlyList<IPlantImageSource> _sources = sources.ToList();

    public async Task<SelectedPlantImages> SelectAsync(string scientificName, CancellationToken cancellationToken) =>
        new(await SelectAsync(scientificName, PlantImageKind.Photo, cancellationToken),
            await SelectAsync(scientificName, PlantImageKind.Illustration, cancellationToken));

    /// <summary>Only the given kinds; sources of other kinds are not asked.</summary>
    /// <param name="excluded">Source images (provider, source ID) never to choose, e.g. ones already stored or rejected.</param>
    public async Task<IReadOnlyList<AcceptedImage>> SelectAsync(
        string scientificName,
        IReadOnlyCollection<PlantImageKind> kinds,
        CancellationToken cancellationToken,
        IReadOnlySet<(string Provider, string SourceId)>? excluded = null)
    {
        var selected = new List<AcceptedImage>();
        foreach (var kind in kinds)
        {
            if (await SelectAsync(scientificName, kind, cancellationToken, excluded) is { } image) selected.Add(image);
        }

        return selected;
    }

    private async Task<AcceptedImage?> SelectAsync(
        string scientificName,
        PlantImageKind kind,
        CancellationToken cancellationToken,
        IReadOnlySet<(string Provider, string SourceId)>? excluded = null)
    {
        foreach (var source in _sources.Where(s => s.Kind == kind))
        {
            foreach (var candidate in await source.GetCandidatesAsync(scientificName, cancellationToken))
            {
                // A source of photos must not slip in a drawing, or the reverse.
                if (candidate.Kind == kind
                    && excluded?.Contains((candidate.Provider, candidate.SourceId)) != true
                    && ExternalImagePolicy.Evaluate(candidate, out var accepted) is null)
                {
                    return accepted;
                }
            }
        }

        return null;
    }
}
