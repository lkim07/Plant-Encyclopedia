using PlantEncyclopedia.Api.Errors;

namespace PlantEncyclopedia.Api.Common;

/// <summary>A validated, 1-based page request.</summary>
public readonly record struct PageRequest(int Page, int PageSize);

/// <summary>The "pagination" object in collection responses (API_SPEC §9–10).</summary>
public sealed record PaginationResponse(int Page, int PageSize, bool HasNext);

/// <summary>Reads ?page and ?pageSize and enforces the documented limits (API_SPEC §10).</summary>
public static class Pagination
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int MaxPage = 10_000;

    /// <summary>
    /// Returns the page request, or a 400 VALIDATION_ERROR result listing each invalid parameter.
    /// The raw strings are parsed here, so malformed values get the documented error body.
    /// </summary>
    public static (PageRequest Request, IResult? Error) Parse(string? page, string? pageSize)
    {
        var details = new Dictionary<string, string>();

        var pageNumber = 1;
        if (page is not null && (!int.TryParse(page, out pageNumber) || pageNumber < 1 || pageNumber > MaxPage))
        {
            details["page"] = $"Page must be a whole number between 1 and {MaxPage}.";
        }

        var size = DefaultPageSize;
        if (pageSize is not null && (!int.TryParse(pageSize, out size) || size < 1 || size > MaxPageSize))
        {
            details["pageSize"] = $"Page size must be a whole number between 1 and {MaxPageSize}.";
        }

        return details.Count > 0
            ? (default, ApiErrors.Validation(details))
            : (new PageRequest(pageNumber, size), null);
    }
}
