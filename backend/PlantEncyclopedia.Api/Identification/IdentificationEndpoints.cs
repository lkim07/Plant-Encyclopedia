using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using PlantEncyclopedia.Api.Accounts;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Api.Search;
using PlantEncyclopedia.Application.Identification;

namespace PlantEncyclopedia.Api.Identification;

/// <summary>JSON contract for one identification candidate (API_SPEC §37). Changing it changes the public API.</summary>
public sealed record IdentificationCandidateResponse(int Rank, IdentifiedPlantResponse Plant);

public sealed record IdentifiedPlantResponse(Guid Id, string Name, string? PlantGroup, string ScientificName, string? ImageUrl);

/// <summary>
/// POST /api/identification (API_SPEC §36–42): a photo in, up to three encyclopedia plants out, ranked, with no
/// confidence values (D-013). Open to everyone (Identify is the home screen), so it is protected by a per-client
/// rate limit, the provider's app-wide budget, a size limit, a timeout and the CSRF token (a multipart POST is
/// otherwise sendable from any site without a CORS preflight, which would spend the budget through visitors'
/// browsers). The photo is processed in memory and never stored or logged.
/// </summary>
public static class IdentificationEndpoints
{
    public const string RateLimitPolicy = "identification";
    public const string TimeoutPolicy = "identification";
    public const int RequestsPerMinute = 6;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    /// <summary>Largest accepted photo. Room is left for the multipart framing around it.</summary>
    public const int MaxImageBytes = 10 * 1024 * 1024;
    private const int MaxBodyBytes = MaxImageBytes + 64 * 1024;

    public const string FailedCode = "IDENTIFICATION_FAILED";
    public const string UnavailableCode = "IDENTIFICATION_UNAVAILABLE";

    public static IServiceCollection AddIdentificationEndpoints(this IServiceCollection services)
    {
        services.Configure<RateLimiterOptions>(options =>
            SearchProtection.AddPerClientPolicy(options, RateLimitPolicy, RequestsPerMinute));
        services.Configure<RequestTimeoutOptions>(options => options.AddPolicy(TimeoutPolicy, new RequestTimeoutPolicy
        {
            Timeout = Timeout,
            TimeoutStatusCode = StatusCodes.Status503ServiceUnavailable,
            WriteTimeoutResponse = context => context.Response.WriteAsJsonAsync(
                new { error = new { code = SearchProtection.TimeoutCode, message = SearchProtection.TimeoutMessage } })
        }));
        return services;
    }

    public static IEndpointRouteBuilder MapIdentificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/identification", IdentifyAsync)
            .RequireCsrfToken()
            .RequireRateLimiting(RateLimitPolicy)
            .WithRequestTimeout(TimeoutPolicy);
        return app;
    }

    // POST /api/identification, multipart/form-data with one file field "image".
    private static async Task<IResult> IdentifyAsync(
        HttpRequest request,
        PlantIdentificationService identification,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes) return ApiErrors.PayloadTooLarge();
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
        {
            size.MaxRequestBodySize = MaxBodyBytes;
        }

        if (!request.HasFormContentType) return ApiErrors.Validation("image", "Send the photo as multipart/form-data.");

        IFormFile? file;
        try
        {
            var form = await request.ReadFormAsync(
                new FormOptions { MultipartBodyLengthLimit = MaxBodyBytes, ValueCountLimit = 8, KeyLengthLimit = 64 },
                cancellationToken);
            file = form.Files.GetFile("image");
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return ApiErrors.PayloadTooLarge();
        }
        catch (InvalidDataException)
        {
            // Malformed multipart, or a part over the limits above.
            return ApiErrors.Validation("image", "The upload could not be read. Choose the photo again.");
        }

        if (file is null || file.Length == 0) return ApiErrors.Validation("image", "Choose a photo.");
        if (file.Length > MaxImageBytes) return ApiErrors.PayloadTooLarge();

        // The file name and declared type are ignored; the bytes decide (SECURITY §10).
        var upload = new byte[file.Length];
        await using (var stream = file.OpenReadStream())
        {
            await stream.ReadExactlyAsync(upload, cancellationToken);
        }

        var result = await identification.IdentifyAsync(upload, cancellationToken);
        return result.Status switch
        {
            IdentificationStatus.Identified => Results.Ok(new
            {
                data = new
                {
                    status = "completed",
                    candidates = result.Candidates
                        .Select((p, index) => new IdentificationCandidateResponse(
                            index + 1, new IdentifiedPlantResponse(p.Id, p.Name, p.PlantGroupName, p.ScientificName, p.ImageUrl)))
                        .ToList()
                }
            }),
            IdentificationStatus.InvalidImage => ApiErrors.Validation("image", "Use a JPEG, PNG or WebP photo at least 64 pixels wide and high."),
            IdentificationStatus.NotIdentified => Results.Json(
                new
                {
                    error = new
                    {
                        code = FailedCode,
                        message = "We couldn't identify this plant. The photo may be unclear, or the plant may not be in our encyclopedia yet."
                    }
                },
                statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => ApiErrors.ServiceUnavailable(UnavailableCode, "Identification is unavailable right now. Please try again later.")
        };
    }
}
