using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantEncyclopedia.Application.Favorites;
using PlantEncyclopedia.Domain.Favorites;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Infrastructure.Persistence;

/// <summary>
/// Favorites in PostgreSQL (DATABASE_DESIGN §26–27). Every change runs in a transaction that first takes a
/// transaction-level advisory lock for the user, so concurrent saves, removals and reorders of one user happen one
/// after another and positions stay 1…n. Other users are never blocked.
/// Favorites of plants that are no longer Published are kept but not shown; they sort after the visible ones.
/// </summary>
internal sealed class FavoriteStore(PlantEncyclopediaDbContext db) : IFavoriteStore
{
    public const string UniqueIndexName = "ux_favorites_user_plant";

    public async Task<IReadOnlyList<FavoriteItem>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.Favorites
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.Plant!.Status == PlantStatus.Published)
            .OrderBy(f => f.Position)
            .ThenBy(f => f.CreatedAt)
            .Select(f => new
            {
                f.Id,
                f.PlantId,
                Name = f.Plant!.DisplayName,
                Group = f.Plant.PlantGroup != null ? f.Plant.PlantGroup.Name : null,
                Image = f.Plant.Images
                    .Where(i => i.RejectedAt == null)
                    .OrderBy(i => i.Kind == PlantImageKind.Photo ? 0 : 1)
                    .ThenBy(i => i.CreatedAt)
                    .Select(i => i.MediumUrl ?? i.Url)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // The order the user sees counts only the plants they can see.
        return rows.Select((r, index) => new FavoriteItem(r.Id, index + 1, r.PlantId, r.Name, r.Group, r.Image)).ToList();
    }

    public async Task<AddFavoriteResult> AddAsync(Guid userId, Guid plantId, int? order, CancellationToken cancellationToken)
    {
        await using var transaction = await LockAsync(userId, cancellationToken);

        if (!await db.Plants.AnyAsync(p => p.Id == plantId && p.Status == PlantStatus.Published, cancellationToken))
        {
            return new AddFavoriteResult(AddFavoriteStatus.PlantNotFound);
        }

        if (await db.Favorites.AnyAsync(f => f.UserId == userId && f.PlantId == plantId, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return new AddFavoriteResult(AddFavoriteStatus.AlreadySaved, await FindAsync(userId, plantId, cancellationToken));
        }

        var count = await db.Favorites.CountAsync(f => f.UserId == userId, cancellationToken);
        if (count >= IFavoriteStore.MaxFavorites)
        {
            return new AddFavoriteResult(AddFavoriteStatus.LimitReached);
        }

        var position = order is { } wanted ? Math.Clamp(wanted, 1, count + 1) : count + 1;
        if (position <= count)
        {
            await db.Favorites
                .Where(f => f.UserId == userId && f.Position >= position)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Position, f => f.Position + 1), cancellationToken);
        }

        db.Favorites.Add(new Favorite { UserId = userId, PlantId = plantId, Position = position });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UniqueIndexName
        })
        {
            // Not expected under the lock; kept so a duplicate can never surface as an error.
            return new AddFavoriteResult(AddFavoriteStatus.AlreadySaved);
        }

        await transaction.CommitAsync(cancellationToken);
        return new AddFavoriteResult(AddFavoriteStatus.Added, await FindAsync(userId, plantId, cancellationToken));
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid favoriteId, CancellationToken cancellationToken)
    {
        await using var transaction = await LockAsync(userId, cancellationToken);

        var favorite = await db.Favorites.SingleOrDefaultAsync(f => f.Id == favoriteId && f.UserId == userId, cancellationToken);
        if (favorite is null) return false;

        db.Favorites.Remove(favorite);
        await db.SaveChangesAsync(cancellationToken);
        await db.Favorites
            .Where(f => f.UserId == userId && f.Position > favorite.Position)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Position, f => f.Position - 1), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<ReorderFavoritesStatus> ReorderAsync(
        Guid userId,
        IReadOnlyList<(Guid FavoriteId, int Order)> items,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LockAsync(userId, cancellationToken);

        var favorites = await db.Favorites
            .Where(f => f.UserId == userId)
            .Select(f => new { Favorite = f, Visible = f.Plant!.Status == PlantStatus.Published })
            .ToListAsync(cancellationToken);
        var visible = favorites.Where(f => f.Visible).Select(f => f.Favorite).ToDictionary(f => f.Id);

        var valid = items.Count == visible.Count
            && items.Select(i => i.FavoriteId).Distinct().Count() == items.Count
            && items.All(i => visible.ContainsKey(i.FavoriteId))
            && items.Select(i => i.Order).Order().SequenceEqual(Enumerable.Range(1, items.Count));
        if (!valid) return ReorderFavoritesStatus.Invalid;

        foreach (var (favoriteId, order) in items)
        {
            visible[favoriteId].Position = order;
        }

        var next = items.Count + 1;
        foreach (var hidden in favorites.Where(f => !f.Visible).Select(f => f.Favorite).OrderBy(f => f.Position))
        {
            hidden.Position = next++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ReorderFavoritesStatus.Reordered;
    }

    private async Task<FavoriteItem?> FindAsync(Guid userId, Guid plantId, CancellationToken cancellationToken) =>
        (await ListAsync(userId, cancellationToken)).SingleOrDefault(f => f.PlantId == plantId);

    // Serializes one user's changes until the transaction ends; the key is derived from the user ID.
    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> LockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var key = userId.ToString();
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        return transaction;
    }
}
