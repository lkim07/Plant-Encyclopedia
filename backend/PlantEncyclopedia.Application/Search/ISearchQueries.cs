namespace PlantEncyclopedia.Application.Search;

public interface ISearchQueries
{
    /// <summary>
    /// Searches Published plants. Every word of <paramref name="text"/> must appear in one of the plant's
    /// names (display, scientific, cultivar, common names, synonyms) or its group's name, ignoring case,
    /// accents and punctuation. Results are ordered by relevance, then name, then ID; page is 1-based.
    /// </summary>
    /// <param name="text">Search text that has already been validated and normalized by the caller.</param>
    Task<SearchPage> SearchAsync(string text, int page, int pageSize, CancellationToken cancellationToken);
}
