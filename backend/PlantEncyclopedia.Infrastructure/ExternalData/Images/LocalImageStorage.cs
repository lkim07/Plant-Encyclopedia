namespace PlantEncyclopedia.Infrastructure.ExternalData.Images;

/// <summary>Where stored image files live. Local folder now; Cloudflare R2 later (Phase 11d-3).</summary>
internal interface IImageStorage
{
    /// <summary>Saves the file under a generated relative path and returns the URL the browser loads it from.</summary>
    Task<string> SaveAsync(string relativePath, byte[] content, CancellationToken cancellationToken);
}

/// <summary>
/// Writes files under <see cref="PlantImageOptions.StoragePath"/>; the API serves them at
/// <see cref="PlantImageOptions.RequestPath"/>. Paths are built by the application from GUIDs and fixed names,
/// never from external text, and are checked to stay inside the folder anyway. Each file is written to a
/// temporary name first and then moved, so a half-written file is never served.
/// </summary>
internal sealed class LocalImageStorage(PlantImageOptions options) : IImageStorage
{
    public async Task<string> SaveAsync(string relativePath, byte[] content, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(options.StoragePath);
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("The image path must stay inside the storage folder.", nameof(relativePath));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp";
        await File.WriteAllBytesAsync(temporary, content, cancellationToken);
        File.Move(temporary, fullPath, overwrite: true);

        return $"{PlantImageOptions.RequestPath}/{relativePath.Replace('\\', '/')}";
    }
}
