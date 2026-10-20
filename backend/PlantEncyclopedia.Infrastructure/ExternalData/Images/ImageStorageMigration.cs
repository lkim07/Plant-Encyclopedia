using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Infrastructure.Persistence;

namespace PlantEncyclopedia.Infrastructure.ExternalData.Images;

/// <summary>The outcome of moving locally stored images to R2.</summary>
/// <param name="Images">Image rows whose URLs now point to R2.</param>
/// <param name="Files">Files uploaded.</param>
/// <param name="MissingFiles">Local files that no longer exist; those URLs were left unchanged.</param>
public sealed record ImageMigrationResult(int Images, int Files, int MissingFiles);

/// <summary>
/// One-off move of images stored in the local folder to Cloudflare R2 (Phase 11d-3): every URL that still starts with
/// <see cref="PlantImageOptions.RequestPath"/> is uploaded under the same key and rewritten to the public R2 address.
/// Safe to run again: rows already on R2 are skipped. The local files are left in place as a backup.
/// </summary>
internal sealed partial class ImageStorageMigration(
    PlantEncyclopediaDbContext db,
    PlantImageOptions options,
    R2ImageStorage r2,
    ILogger<ImageStorageMigration> logger)
{
    private const string LocalPrefix = PlantImageOptions.RequestPath + "/";

    public async Task<ImageMigrationResult> MigrateAsync(CancellationToken cancellationToken)
    {
        var rows = await db.PlantImages
            .Where(i => i.Url.StartsWith(LocalPrefix)
                || (i.ThumbnailUrl != null && i.ThumbnailUrl.StartsWith(LocalPrefix))
                || (i.MediumUrl != null && i.MediumUrl.StartsWith(LocalPrefix))
                || (i.LargeUrl != null && i.LargeUrl.StartsWith(LocalPrefix)))
            .ToListAsync(cancellationToken);

        var uploaded = new Dictionary<string, string>(StringComparer.Ordinal);
        var missing = 0;

        async Task<string?> MoveAsync(string? url)
        {
            if (url is null || !url.StartsWith(LocalPrefix, StringComparison.Ordinal)) return url;
            if (uploaded.TryGetValue(url, out var moved)) return moved;

            var key = url[LocalPrefix.Length..];
            var root = Path.GetFullPath(options.StoragePath);
            var file = Path.GetFullPath(Path.Combine(root, key));
            if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(file))
            {
                missing++;
                return url;
            }

            moved = await r2.SaveAsync(key, await File.ReadAllBytesAsync(file, cancellationToken), cancellationToken);
            uploaded[url] = moved;
            return moved;
        }

        foreach (var row in rows)
        {
            row.ThumbnailUrl = await MoveAsync(row.ThumbnailUrl);
            row.MediumUrl = await MoveAsync(row.MediumUrl);
            row.LargeUrl = await MoveAsync(row.LargeUrl);
            row.Url = (await MoveAsync(row.Url))!;
            await db.SaveChangesAsync(cancellationToken);
        }

        LogMigrated(logger, rows.Count, uploaded.Count, missing);
        return new ImageMigrationResult(rows.Count, uploaded.Count, missing);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Moved {Images} images to R2: {Files} files uploaded, {Missing} local files missing.")]
    private static partial void LogMigrated(ILogger logger, int images, int files, int missing);
}

public static class ImageStorageMigrationExtensions
{
    /// <summary>Runs the one-off local-to-R2 move. Requires image storage to be configured as R2.</summary>
    public static async Task<ImageMigrationResult> MigrateImagesToR2Async(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var migration = scope.ServiceProvider.GetService<ImageStorageMigration>()
            ?? throw new InvalidOperationException(
                $"Image storage is not R2. Set {PlantImageOptions.SectionName}:Enabled to true and Storage to R2 first.");
        return await migration.MigrateAsync(cancellationToken);
    }
}
