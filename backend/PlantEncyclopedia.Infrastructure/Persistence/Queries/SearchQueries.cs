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
        var words = await FoldAsync(text, cancellationToken);
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
                p.ScientificName,
                p.Images
                    .OrderBy(i => i.Kind == PlantImageKind.Photo ? 0 : 1)
                    .ThenBy(i => i.CreatedAt)
                    .Select(i => i.MediumUrl ?? i.Url)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var hasNext = rows.Count > pageSize;
        return new SearchPage(hasNext ? rows.Take(pageSize).ToList() : rows, hasNext);
    }

    public async Task<IReadOnlyList<SearchSuggestion>> SuggestAsync(
        string text,
        int limit,
        CancellationToken cancellationToken)
    {
        var key = string.Join(' ', await FoldAsync(text, cancellationToken));
        if (key.Length < ISearchQueries.MinSuggestionLength)
        {
            return [];
        }

        // A name matches when it starts with the text, or one of its later words does.
        var startsWith = EscapeLike(key) + "%";
        var wordStartsWith = "% " + EscapeLike(key) + "%";
        var published = PlantStatus.Published.ToString();
        var common = PlantNameType.Common.ToString();

        // One parameterized statement: every value below is sent as a bound parameter, never spliced into SQL.
        // Candidate names are folded with search_key(), so the trigram indexes serve the LIKE filters.
        // Names that fold to the same key are merged; the lowest type code supplies the shown text and type.
        var rows = await db.Database.SqlQuery<SuggestionRow>($"""
            WITH candidates AS (
                SELECT p.display_name AS text, 1 AS type_code, public.search_key(p.display_name) AS key
                FROM plants p WHERE p.status = {published}
                UNION ALL
                SELECT p.cultivar_name, 2, public.search_key(p.cultivar_name)
                FROM plants p WHERE p.status = {published} AND p.cultivar_name IS NOT NULL
                UNION ALL
                SELECT p.scientific_name, 3, public.search_key(p.scientific_name)
                FROM plants p WHERE p.status = {published}
                UNION ALL
                SELECT n.name, CASE WHEN n.name_type = {common} THEN 4 ELSE 5 END, public.search_key(n.name)
                FROM plant_names n JOIN plants p ON p.id = n.plant_id WHERE p.status = {published}
                UNION ALL
                SELECT g.name, 6, public.search_key(g.name)
                FROM plant_groups g
                WHERE EXISTS (SELECT 1 FROM plants p WHERE p.plant_group_id = g.id AND p.status = {published})
            )
            SELECT
                (array_agg(c.text ORDER BY c.type_code, c.text))[1] AS text,
                min(c.type_code) AS type_code,
                bool_or(c.key LIKE {startsWith} ESCAPE '\') AS starts_with
            FROM candidates c
            WHERE c.key LIKE {startsWith} ESCAPE '\' OR c.key LIKE {wordStartsWith} ESCAPE '\'
            GROUP BY c.key
            ORDER BY starts_with DESC, length(c.key), c.key
            LIMIT {limit}
            """).ToListAsync(cancellationToken);

        return rows.Select(r => new SearchSuggestion(r.Text, (SuggestionType)r.TypeCode)).ToList();
    }

    /// <summary>
    /// Folds the text in the database exactly as stored names are folded (search_key), so "CAFÉ, Mr." and
    /// "cafe mr" compare equal, and returns its words.
    /// </summary>
    private async Task<string[]> FoldAsync(string text, CancellationToken cancellationToken)
    {
        var folded = await db.Database
            .SqlQuery<string>($"SELECT public.search_key({text}) AS \"Value\"")
            .SingleAsync(cancellationToken);

        return folded.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(MaxWords).ToArray();
    }

    private sealed class SuggestionRow
    {
        public required string Text { get; init; }
        public int TypeCode { get; init; }
        public bool StartsWith { get; init; }
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
