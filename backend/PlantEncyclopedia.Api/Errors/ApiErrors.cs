namespace PlantEncyclopedia.Api.Errors;

/// <summary>Builds error responses in the documented format: {"error":{"code","message","details"?}} (API_SPEC §12).</summary>
public static class ApiErrors
{
    public static IResult NotFound(string code, string message) =>
        Results.NotFound(new { error = new { code, message } });

    public static IResult Validation(string field, string fieldMessage) =>
        Results.BadRequest(new
        {
            error = new
            {
                code = "VALIDATION_ERROR",
                message = "One or more fields are invalid.",
                details = new Dictionary<string, string> { [field] = fieldMessage }
            }
        });
}
