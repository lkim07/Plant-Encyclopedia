using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PlantEncyclopedia.Application.History;
using PlantEncyclopedia.Domain.History;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence;

/// <summary>
/// Search and view history in PostgreSQL (DATABASE_DESIGN §28–30). Each write runs in a transaction that first takes
/// an advisory lock for the user's history, so two tabs cannot add the same search twice or skip trimming. Only the
/// newest <see cref="IHistoryStore.MaxSearches"/> searches and <see cref="IHistoryStore.MaxViews"/> views are kept.
/// Search text is personal data: it is stored for its user only and never logged.
/// </summary>
internal sealed class HistoryStore(PlantEncyclopediaDbContext db, TimeProvider time) : IHistoryStore
{
    public async Task<IReadOnlyList<SearchHistoryItem>> ListSearchesAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.SearchHistory
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SearchedAt)
            .ThenByDescending(s => s.Id)
            .Take(IHistoryStore.RecentSearchLimit)
            .Select(s => new SearchHistoryItem(s.Id, s.Query, s.SearchedAt))
            .ToListAsync(cancellationToken);

    public async Task<SearchHistoryItem> AddSearchAsync(Guid userId, string query, CancellationToken cancellationToken)
    {
        await using var transaction = await LockAsync(userId, cancellationToken);

        var key = query.ToLowerInvariant();
        var entry = await db.SearchHistory.SingleOrDefaultAsync(s => s.UserId == userId && s.QueryKey == key, cancellationToken);
        if (entry is null)
        {
            entry = new SearchHistoryEntry { UserId = userId, Query = query, QueryKey = key };
            db.SearchHistory.Add(entry);
        }

        entry.Query = query;
        entry.SearchedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var keep = db.SearchHistory
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SearchedAt)
            .ThenByDescending(s => s.Id)
            .Take(IHistoryStore.MaxSearches)
            .Select(s => s.Id);
        await db.SearchHistory
            .Where(s => s.UserId == userId && !keep.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new SearchHistoryItem(entry.Id, entry.Query, entry.SearchedAt);
    }

    public async Task<bool> RemoveSearchAsync(Guid userId, Guid searchId, CancellationToken cancellationToken) =>
        await db.SearchHistory
            .Where(s => s.Id == searchId && s.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<bool> AddViewAsync(Guid userId, Guid plantId, CancellationToken cancellationToken)
    {
        if (!await db.Plants.AnyAsync(p => p.Id == plantId && p.Status == PlantStatus.Published, cancellationToken))
        {
            return false;
        }

        await using var transaction = await LockAsync(userId, cancellationToken);

        var now = time.GetUtcNow();
        var recent = now.AddMinutes(-IHistoryStore.ViewRepeatMinutes);
        if (await db.ViewHistory.AnyAsync(v => v.UserId == userId && v.PlantId == plantId && v.ViewedAt > recent, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        db.ViewHistory.Add(new ViewHistoryEntry { UserId = userId, PlantId = plantId, ViewedAt = now });
        await db.SaveChangesAsync(cancellationToken);

        var keep = db.ViewHistory
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.ViewedAt)
            .ThenByDescending(v => v.Id)
            .Take(IHistoryStore.MaxViews)
            .Select(v => v.Id);
        await db.ViewHistory
            .Where(v => v.UserId == userId && !keep.Contains(v.Id))
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // Serializes one user's history writes until the transaction ends. The key differs from the favorites lock,
    // so history never waits for favorites.
    private async Task<IDbContextTransaction> LockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var key = $"history:{userId}";
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        return transaction;
    }
}
