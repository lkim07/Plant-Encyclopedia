using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Application.ExternalData.Images;

/// <summary>What the image import needs to know about a plant.</summary>
/// <param name="ExistingKinds">Kinds the plant already has an image of; they are not fetched again.</param>
public sealed record PlantImageTarget(
    string DisplayName,
    string ScientificName,
    string? CultivarName,
    IReadOnlyList<PlantImageKind> ExistingKinds);

/// <summary>Database operations for image import (implemented in Infrastructure).</summary>
public interface IPlantImageStore
{
    Task<PlantImageTarget?> FindTargetAsync(Guid plantId, CancellationToken cancellationToken);

    /// <summary>Saves the image row. Returns false when the same source image is already stored for the plant.</summary>
    Task<bool> AddAsync(PlantImage image, CancellationToken cancellationToken);

    /// <summary>Species-level plants (no cultivar) that are not Archived and have no photo, oldest first.</summary>
    Task<IReadOnlyList<Guid>> FindPlantsWithoutImagesAsync(int limit, CancellationToken cancellationToken);
}

/// <summary>The stored renditions of one image, as URLs the browser can load.</summary>
/// <param name="Width">Width of the largest rendition.</param>
/// <param name="Height">Height of the largest rendition.</param>
public sealed record StoredImageFiles(string ThumbnailUrl, string MediumUrl, string LargeUrl, int Width, int Height);

/// <summary>
/// Downloads an accepted image, checks it really is one, re-encodes it into the stored sizes and saves them
/// (implemented in Infrastructure). Returns null on any failure; nothing is saved then.
/// </summary>
public interface IImageFileStore
{
    Task<StoredImageFiles?> SaveAsync(Guid plantId, Guid imageId, AcceptedImage image, CancellationToken cancellationToken);
}

/// <summary>Plants waiting for image import; processed in the background so lookups are not slowed down.</summary>
public interface IPlantImageQueue
{
    void Enqueue(Guid plantId);
}

public enum PlantImageImportStatus
{
    /// <summary>The plant does not exist.</summary>
    NotFound,

    /// <summary>A cultivar: a species image would show the wrong plant, so none is fetched.</summary>
    Skipped,

    /// <summary>Every kind was already present or was looked up; see the counts.</summary>
    Done
}

public sealed record PlantImageImportResult(PlantImageImportStatus Status, int Added = 0);

/// <summary>
/// Gives a plant a photo automatically, or a botanical illustration when no photo can be found (ARCHITECTURE
/// §22). A plant that already has a photo is left alone; an illustration is looked up only when the photo lookup
/// finds nothing and the plant has none. Images are chosen by <see cref="PlantImageSelector"/>, stored by
/// <see cref="IImageFileStore"/>, and recorded with their licence code, author and source page.
/// </summary>
public sealed class PlantImageImportService(
    PlantImageSelector selector,
    IPlantImageStore store,
    IImageFileStore files,
    TimeProvider timeProvider)
{
    public async Task<PlantImageImportResult> ImportAsync(Guid plantId, CancellationToken cancellationToken)
    {
        var target = await store.FindTargetAsync(plantId, cancellationToken);
        if (target is null) return new PlantImageImportResult(PlantImageImportStatus.NotFound);
        if (target.CultivarName is not null) return new PlantImageImportResult(PlantImageImportStatus.Skipped);

        if (target.ExistingKinds.Contains(PlantImageKind.Photo)) return new PlantImageImportResult(PlantImageImportStatus.Done);

        var images = await selector.SelectAsync(target.ScientificName, [PlantImageKind.Photo], cancellationToken);
        if (images.Count == 0 && !target.ExistingKinds.Contains(PlantImageKind.Illustration))
        {
            images = await selector.SelectAsync(target.ScientificName, [PlantImageKind.Illustration], cancellationToken);
        }

        var added = 0;
        foreach (var image in images)
        {
            var imageId = Guid.NewGuid();
            var stored = await files.SaveAsync(plantId, imageId, image, cancellationToken);
            if (stored is null) continue;

            var saved = await store.AddAsync(new PlantImage
            {
                Id = imageId,
                PlantId = plantId,
                Kind = image.Image.Kind,
                Url = stored.LargeUrl,
                ThumbnailUrl = stored.ThumbnailUrl,
                MediumUrl = stored.MediumUrl,
                LargeUrl = stored.LargeUrl,
                Width = stored.Width,
                Height = stored.Height,
                Source = image.Image.Provider,
                SourceId = image.Image.SourceId,
                SourceUrl = image.Image.SourcePageUrl?.AbsoluteUri,
                License = image.LicenseCode,
                Attribution = image.Creator,
                AltText = AltText(target, image.Image.Kind),
                RetrievedAt = timeProvider.GetUtcNow()
            }, cancellationToken);

            if (saved) added++;
        }

        return new PlantImageImportResult(PlantImageImportStatus.Done, added);
    }

    // "Dog rose (Rosa canina)", or "Botanical illustration of Dog rose (Rosa canina)".
    private static string AltText(PlantImageTarget target, PlantImageKind kind)
    {
        var name = target.DisplayName == target.ScientificName
            ? target.ScientificName
            : $"{target.DisplayName} ({target.ScientificName})";
        return kind == PlantImageKind.Illustration ? $"Botanical illustration of {name}" : name;
    }
}
