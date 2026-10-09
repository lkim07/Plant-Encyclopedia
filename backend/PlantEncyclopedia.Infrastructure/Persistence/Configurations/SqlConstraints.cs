namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared SQL fragments for CHECK constraints. Column names are snake_case because
/// raw SQL is not rewritten by the naming convention.
/// </summary>
internal static class SqlConstraints
{
    public const string SlugPattern = "'^[a-z0-9]+(-[a-z0-9]+)*$'";

    // General shape of a BCP 47 language tag, e.g. en, en-GB, zh-Hant, es-419.
    // Canonical casing is left to application-level validation.
    public const string LanguageTagPattern = "'^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$'";

    /// <summary>
    /// Builds "column IN ('A', 'B', ...)" from an enum stored as its member names.
    /// </summary>
    public static string InEnum<TEnum>(string column) where TEnum : struct, Enum =>
        $"{column} IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"))})";
}
