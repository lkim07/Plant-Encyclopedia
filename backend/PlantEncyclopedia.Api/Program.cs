using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using PlantEncyclopedia.Api.Accounts;
using PlantEncyclopedia.Api.Categories;
using PlantEncyclopedia.Api.Dev;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Api.Explore;
using PlantEncyclopedia.Api.Favorites;
using PlantEncyclopedia.Api.History;
using PlantEncyclopedia.Api.Health;
using PlantEncyclopedia.Api.PlantGroups;
using PlantEncyclopedia.Api.Plants;
using PlantEncyclopedia.Api.Search;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Infrastructure;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;
using PlantEncyclopedia.Infrastructure.ExternalData.Images;
using PlantEncyclopedia.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Local development: ASP.NET Core User Secrets. Other environments: ConnectionStrings__PlantEncyclopedia.
var connectionString = builder.Configuration.GetConnectionString("PlantEncyclopedia")
    ?? throw new InvalidOperationException(
        "Connection string 'PlantEncyclopedia' is not configured. " +
        "Set it with User Secrets or the ConnectionStrings__PlantEncyclopedia environment variable.");

builder.Services.AddInfrastructure(connectionString);

// External plant import (POST /api/search/external) and image import. Tests never call external services.
var gbif = builder.Configuration.GetSection(GbifOptions.SectionName).Get<GbifOptions>() ?? new GbifOptions();
var images = builder.Configuration.GetSection(PlantImageOptions.SectionName).Get<PlantImageOptions>() ?? new PlantImageOptions();
images.StoragePath = Path.GetFullPath(images.StoragePath, builder.Environment.ContentRootPath);
if (builder.Environment.IsEnvironment("Testing"))
{
    gbif.Enabled = false;
    images.Enabled = false;
}

builder.Services.AddExternalPlantData(
    gbif,
    new GenusPlantGroupMap(builder.Configuration.GetSection("ExternalPlantData:GenusPlantGroups").Get<Dictionary<string, string>>()),
    images);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSearchProtection();
builder.Services.AddAccounts(builder.Configuration, builder.Environment);
builder.Services.AddFavorites();
builder.Services.AddHistory();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PlantEncyclopediaDbContext>("database");

var app = builder.Build();

// Connection check: upload, read back through the public address, and delete one tiny file in R2, then exit.
//   dotnet run --project PlantEncyclopedia.Api -- check-r2
if (args.Contains("check-r2"))
{
    var check = await app.Services.CheckR2Async(CancellationToken.None);
    Console.WriteLine(
        $"R2 check: uploaded={check.Uploaded} publicReadMatches={check.PublicReadMatches} deleted={check.Deleted} " +
        $"url={check.PublicUrl} {check.Problem}");
    return;
}

// One-off command: move locally stored images to Cloudflare R2, then exit without starting the web server.
//   dotnet run --project PlantEncyclopedia.Api -- migrate-images-to-r2
if (args.Contains("migrate-images-to-r2"))
{
    var moved = await app.Services.MigrateImagesToR2Async(CancellationToken.None);
    Console.WriteLine($"Moved {moved.Images} images to R2: {moved.Files} files uploaded, {moved.MissingFiles} local files missing.");
    return;
}

// GlobalExceptionHandler writes the response. The empty fallback is required by the middleware and
// only runs if GlobalExceptionHandler does not handle the exception; it returns a bare 500 with no body.
app.UseExceptionHandler(_ => { });
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Stored plant images (ARCHITECTURE §22). File names contain a new GUID per image, so they never change and can
// be cached for a year. Only .webp files are served.
if (images.Enabled) Directory.CreateDirectory(images.StoragePath);
if (Directory.Exists(images.StoragePath))
{
    var webpOnly = new FileExtensionContentTypeProvider();
    webpOnly.Mappings.Clear();
    webpOnly.Mappings[".webp"] = "image/webp";

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(images.StoragePath),
        RequestPath = PlantImageOptions.RequestPath,
        ContentTypeProvider = webpOnly,
        OnPrepareResponse = context =>
        {
            context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            context.Context.Response.Headers.XContentTypeOptions = "nosniff";
        }
    });
}

app.UseRequestTimeouts();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapPlantEndpoints();
app.MapCategoryEndpoints();
app.MapPlantGroupEndpoints();
app.MapSearchEndpoints();
app.MapExploreEndpoints();
app.MapAccountEndpoints();
app.MapFavoriteEndpoints();
app.MapHistoryEndpoints();

// Unauthenticated development tools (image review). Never mapped in other environments.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapDevImageEndpoints();
}

app.Run();

// Lets the integration tests start the API in memory (WebApplicationFactory<Program>).
public partial class Program;
