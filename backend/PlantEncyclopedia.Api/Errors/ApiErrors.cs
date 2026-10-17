namespace PlantEncyclopedia.Api.Errors;

/// <summary>Builds error responses in the documented format: {"error":{"code","message","details"?}} (API_SPEC §12).</summary>
public static class ApiErrors
{
    public static IResult NotFound(string code, string message) =>
        Results.NotFound(new { error = new { code, message } });

    public static IResult PayloadTooLarge() =>
        Results.Json(
            new { error = new { code = "PAYLOAD_TOO_LARGE", message = "The request body is too large." } },
            statusCode: StatusCodes.Status413PayloadTooLarge);

    public static IResult ServiceUnavailable(string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: StatusCodes.Status503ServiceUnavailable);

    public static IResult Validation(string field, string fieldMessage) =>
        Validation(new Dictionary<string, string> { [field] = fieldMessage });

    public static IResult Validation(IReadOnlyDictionary<string, string> details) =>
        Results.BadRequest(new
        {
            error = new
            {
                code = "VALIDATION_ERROR",
                message = "One or more fields are invalid.",
                details
            }
        });
}
