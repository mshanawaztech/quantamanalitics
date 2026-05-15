using Microsoft.AspNetCore.Http.HttpResults;
using QuantamAnalytics.Infrastructure.Features;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Feature-flag snapshot for the SPA. The Angular app reads this once at
/// session start and gates risky UI (e.g. a half-shipped feature behind
/// <c>Features:CandidatePortalV3</c>) on the response.
///
/// The list of exposed flags is intentionally explicit — we don't echo
/// the whole config tree, only the names the frontend asks for. That
/// keeps the response payload predictable and stops accidental leakage
/// of unrelated config (connection strings, secrets) if a future
/// implementation switches to bulk-binding the section.
/// </summary>
public static class FeaturesEndpoint
{
    /// <summary>
    /// The flags the SPA understands. Adding a new flag to the platform
    /// is two steps: (1) add the name here, (2) consume it in the SPA's
    /// FeatureService. Forgetting (1) means the SPA reads it as
    /// "disabled", which is the safe default.
    /// </summary>
    private static readonly string[] PublicFlags =
    [
        "CandidatePortalV3",
        "RecruiterAiCopilot",
        "InvoiceAutomation",
        "PlatformApiBeta",
    ];

    public static IEndpointRouteBuilder MapFeaturesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me/features", GetAsync)
            .WithTags("Feature flags")
            .RequireAuthorization();

        return app;
    }

    private static Ok<FeatureSnapshot> GetAsync(IFeatureGate gate)
    {
        var flags = PublicFlags.ToDictionary(
            name => name,
            name => gate.IsEnabled(name),
            StringComparer.Ordinal);

        return TypedResults.Ok(new FeatureSnapshot(flags));
    }
}

public sealed record FeatureSnapshot(IReadOnlyDictionary<string, bool> Flags);
