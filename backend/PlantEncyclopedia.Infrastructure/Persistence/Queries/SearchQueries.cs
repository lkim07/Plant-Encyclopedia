using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Application.Search;
using PlantEncyclopedia.Domain.Plants;
using static PlantEncyclopedia.Infrastructure.Persistence.SearchFunctions;

namespace PlantEncyclopedia.Infrastructure.Persistence.Queries;

internal sealed class SearchQueries(PlantEncyclopediaDbContext db) : ISearchQueries
{
    /// <summary>Upper bound on words matched, whatever the caller validated (each word adds query work).</summary>
    public const int MaxWords = 10;

    private const string LikeEscape = "\\";

    // All user text reaches SQL only as bound parameters; no SQL is built from it.
    public async Task<SearchPage> SearchAsync(
        string text,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // The database folds the search text exactly as it folds stored names (search_key),
        // so "CAFÉ, Mr." and "cafe mr" compare equal.
        var folded = await db.Database
            .SqlQuery<string>($"SELECT public.search_key({text}) AS \"Value\"")
            .SingleAsync(cancellationToken);

        var words = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(MaxWords).ToArray();
        if (words.Length == 0)
        {
            return new SearchPage([], HasNext: false);
        }

        var key = string.Join(' ', words);
        var startsWithKey = EscapeLike(key) + "%";
        var containsPatterns = words.Select(word => "%" + EscapeLike(word) + "%").ToArray();

        var plants = db.Plants
            .AsNoTracking()
            .Where(p => p.Status == PlantStatus.Published);

        // Every word must appear somewhere.
        foreach (var pattern in containsPatterns)
        {
            plants = plants.Where(AnyNameContains(pattern));
        }

        var rank = Rank(
            NameEquals(key),
            MainNameStartsWith(startsWithKey),
            containsPatterns.Select(MainNamesContain).Aggregate(AndAlso));

        var rows = await plants
            .OrderBy(rank)
            .ThenBy(p => p.DisplayName)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1) // one extra row tells us whether another page exists
            .Select(p => new SearchResult(
                p.Id,
                p.DisplayName,
                p.PlantGroup != null ? p.PlantGroup.Name : null,
                p.ScientificName))
            .ToListAsync(cancellationToken);

        var hasNext = rows.Count > pageSize;
        return new SearchPage(hasNext ? rows.Take(pageSize).ToList() : rows, hasNext);
    }

    /// <summary>The word appears in any searchable name of the plant or its group.</summary>
    private static Expression<Func<Plant, bool>> AnyNameContains(string pattern) => p =>
        EF.Functions.Like(SearchKey(p.DisplayName), pattern, LikeEscape)
        || EF.Functions.Like(SearchKey(p.ScientificName), pattern, LikeEscape)
        || EF.Functions.Like(SearchKey(p.CultivarName), pattern, LikeEscape)
        || p.Names.Any(n => EF.Functions.Like(SearchKey(n.Name), pattern, LikeEscape))
        || (p.PlantGroup != null && EF.Functions.Like(SearchKey(p.PlantGroup.Name), pattern, LikeEscape));

    /// <summary>Rank 0: a main name or a common name equals the whole search.</summary>
    private static Expression<Func<Plant, bool>> NameEquals(string key) => p =>
        SearchKey(p.DisplayName) == key
        || SearchKey(p.CultivarName) == key
        || SearchKey(p.ScientificName) == key
        || p.Names.Any(n => n.NameType == PlantNameType.Common && SearchKey(n.Name) == key);

    /// <summary>Rank 1: a main name starts with the search.</summary>
    private static Expression<Func<Plant, bool>> MainNameStartsWith(string pattern) => p =>
        EF.Functions.Like(SearchKey(p.DisplayName), pattern, LikeEscape)
        || EF.Functions.Like(SearchKey(p.CultivarName), pattern, LikeEscape)
        || EF.Functions.Like(SearchKey(p.ScientificName), pattern, LikeEscape);

    /// <summary>Rank 2 (combined for all words): the word appears in a main name.</summary>
    private static Expression<Func<Plant, bool>> MainNamesContain(string pattern) => p =>
        EF.Functions.Like(SearchKey(p.DisplayName), pattern, LikeEscape)
        || EF.Functions.Like(SearchKey(p.CultivarName), pattern, LikeEscape)
        || EF.Functions.Like(SearchKey(p.ScientificName), pattern, LikeEscape);

    /// <summary>Builds p => equals ? 0 : startsWith ? 1 : allInMainNames ? 2 : 3 (otherwise: other names or group).</summary>
    private static Expression<Func<Plant, int>> Rank(
        Expression<Func<Plant, bool>> equals,
        Expression<Func<Plant, bool>> startsWith,
        Expression<Func<Plant, bool>> allInMainNames)
    {
        var plant = Expression.Parameter(typeof(Plant), "p");
        var body = Expression.Condition(
            Body(equals, plant),
            Expression.Constant(0),
            Expression.Condition(
                Body(startsWith, plant),
                Expression.Constant(1),
                Expression.Condition(Body(allInMainNames, plant), Expression.Constant(2), Expression.Constant(3))));
        return Expression.Lambda<Func<Plant, int>>(body, plant);
    }

    private static Expression<Func<Plant, bool>> AndAlso(
        Expression<Func<Plant, bool>> left,
        Expression<Func<Plant, bool>> right)
    {
        var plant = Expression.Parameter(typeof(Plant), "p");
        return Expression.Lambda<Func<Plant, bool>>(
            Expression.AndAlso(Body(left, plant), Body(right, plant)),
            plant);
    }

    private static Expression Body(Expression<Func<Plant, bool>> lambda, ParameterExpression plant) =>
        new ReplaceParameter(lambda.Parameters[0], plant).Visit(lambda.Body);

    /// <summary>
    /// Makes LIKE treat \, % and _ literally. search_key() already turns them into spaces,
    /// so this is defence in depth.
    /// </summary>
    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
