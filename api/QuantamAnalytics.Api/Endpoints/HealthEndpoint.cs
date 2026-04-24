using System.Reflection;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Liveness/version endpoint. Used by:
///   - The Angular client's HealthService on cold-start to verify the API is reachable.
///   - Azure Container Apps probes (PR-04) and uptime monitors.
///
/// Intentionally unauthenticated: returns no tenant or user data.
/// </summary>
public static class HealthEndpoint
{
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () =>
        {
            var assembly = typeof(HealthEndpoint).Assembly;
            var version =
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "0.0.0";

            // Strip any "+commitsha" suffix the SDK appends to InformationalVersion.
            var plusIndex = version.IndexOf('+');
            if (plusIndex > 0)
            {
                version = version[..plusIndex];
            }

            return Results.Ok(new HealthResponse(
                Status: "ok",
                Service: "quantamanalitics-api",
                Version: version,
                Timestamp: DateTimeOffset.UtcNow));
        })
        .WithName("Health")
        .WithTags("System")
        .AllowAnonymous();

        return app;
    }
}

/// <summary>
/// Stable shape consumed by the Angular HealthService. Don't rename fields
/// without updating client/src/app/core/health/health.service.ts.
/// </summary>
public sealed record HealthResponse(
    string Status,
    string Service,
    string Version,
    DateTimeOffset Timestamp);
