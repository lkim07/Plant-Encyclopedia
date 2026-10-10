using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PlantEncyclopedia.Api.Health;

/// <summary>
/// Writes the documented health body: {"status":"healthy"} or {"status":"unhealthy"}.
/// Check names, durations and exception details are deliberately omitted (API_SPEC §68);
/// failure details are logged server-side by the health check service.
/// </summary>
public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var status = report.Status == HealthStatus.Healthy ? "healthy" : "unhealthy";
        return context.Response.WriteAsJsonAsync(new { status });
    }
}
