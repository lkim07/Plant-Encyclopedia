namespace PlantEncyclopedia.Infrastructure.Persistence;

/// <summary>
/// LINQ stubs for database functions created by the AddSearchSupport migration.
/// They only work inside EF Core queries, which translate them to SQL.
/// </summary>
internal static class SearchFunctions
{
    /// <summary>
    /// <c>public.search_key(text)</c>: removes accents, lower-cases, and turns runs of whitespace and
    /// punctuation into single spaces, so "Café 'Mr. Lincoln'" becomes "cafe mr lincoln".
    /// Trigram indexes on this expression keep "contains" searches fast.
    /// </summary>
    public static string? SearchKey(string? value) =>
        throw new NotSupportedException("SearchKey can only be used in EF Core queries.");
}
