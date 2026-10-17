using PlantEncyclopedia.Application.ExternalData.Images;

namespace PlantEncyclopedia.Infrastructure.ExternalData.Wikimedia;

/// <summary>
/// The main image of the plant's English Wikipedia article, as a photo (ARCHITECTURE §21). Two GETs: the
/// article's page image, then the file's licence on Commons. Redirects are followed ("Mentha × piperita" →
/// "Peppermint"), except to the genus article, whose image could show another species.
/// </summary>
internal sealed class WikipediaPhotoSource(WikimediaClient wikimedia) : IPlantImageSource
{
    public ImageKind Kind => ImageKind.Photo;

    public async Task<IReadOnlyList<ExternalImageCandidate>> GetCandidatesAsync(string scientificName, CancellationToken cancellationToken)
    {
        var (fileName, articleTitle) = await wikimedia.GetPageImageAsync(scientificName, cancellationToken);
        var genus = scientificName.Split(' ')[0];
        if (fileName is null || articleTitle == genus) return [];

        return await wikimedia.GetFileAsync(fileName, ImageKind.Photo, cancellationToken);
    }
}

/// <summary>
/// Botanical illustrations from the Commons category "{scientific name} - botanical illustrations", a Commons
/// naming convention (ARCHITECTURE §21). One GET returning up to 10 files with their licences.
/// </summary>
internal sealed class CommonsIllustrationSource(WikimediaClient wikimedia) : IPlantImageSource
{
    public ImageKind Kind => ImageKind.Illustration;

    public Task<IReadOnlyList<ExternalImageCandidate>> GetCandidatesAsync(string scientificName, CancellationToken cancellationToken) =>
        wikimedia.GetCategoryFilesAsync($"{scientificName} - botanical illustrations", ImageKind.Illustration, cancellationToken);
}
