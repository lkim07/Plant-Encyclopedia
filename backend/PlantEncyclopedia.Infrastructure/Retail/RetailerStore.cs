using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.Retail;
using PlantEncyclopedia.Domain.Retail;
using PlantEncyclopedia.Infrastructure.Persistence;

namespace PlantEncyclopedia.Infrastructure.Retail;

/// <summary>
/// Replaces one source's shops with a new snapshot in one transaction: known records (same source ID) are updated,
/// new ones added, and records the source no longer has are removed. Shops from other sources are untouched.
/// </summary>
internal sealed class RetailerStore(PlantEncyclopediaDbContext db) : IRetailerStore
{
    public Task<int> CountAsync(string source, CancellationToken cancellationToken) =>
        db.Retailers.CountAsync(r => r.Source == source, cancellationToken);

    public async Task<RetailerImportSummary> ReplaceAsync(RetailerSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var stored = await db.Retailers.Where(r => r.Source == snapshot.Source).ToDictionaryAsync(r => r.SourceId, cancellationToken);
        var (added, updated) = (0, 0);
        foreach (var record in snapshot.Records)
        {
            if (!stored.Remove(record.SourceId, out var retailer))
            {
                retailer = new Retailer { Source = snapshot.Source, SourceId = record.SourceId, Name = record.Name };
                db.Retailers.Add(retailer);
                added++;
            }
            else
            {
                updated++;
            }

            retailer.Name = record.Name;
            retailer.Kind = record.Kind;
            retailer.Street = record.Street;
            retailer.City = record.City;
            retailer.Region = record.Region;
            retailer.Postcode = record.Postcode;
            retailer.Website = record.Website;
            retailer.Latitude = record.Latitude;
            retailer.Longitude = record.Longitude;
            retailer.RetrievedAt = snapshot.RetrievedAt;
        }

        // What is left was not in the snapshot: closed or removed at the source.
        db.Retailers.RemoveRange(stored.Values);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RetailerImportSummary(added, updated, stored.Count);
    }
}
