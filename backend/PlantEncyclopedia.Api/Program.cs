using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Api.Health;
using PlantEncyclopedia.Api.Plants;
using PlantEncyclopedia.Infrastructure;
using PlantEncyclopedia.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Local development: ASP.NET Core User Secrets. Other environments: ConnectionStrings__PlantEncyclopedia.
var connectionString = builder.Configuration.GetConnectionString("PlantEncyclopedia")
    ?? throw new InvalidOperationException(
        "Connection string 'PlantEncyclopedia' is not configured. " +
        "Set it with User Secrets or the ConnectionStrings__PlantEncyclopedia environment variable.");

builder.Services.AddInfrastructure(connectionString);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PlantEncyclopediaDbContext>("database");

var app = builder.Build();

// GlobalExceptionHandler writes the response. The empty fallback is required by the middleware and
// only runs if GlobalExceptionHandler does not handle the exception; it returns a bare 500 with no body.
app.UseExceptionHandler(_ => { });
app.UseHttpsRedirection();

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

app.Run();

// Lets the integration tests start the API in memory (WebApplicationFactory<Program>).
public partial class Program;
