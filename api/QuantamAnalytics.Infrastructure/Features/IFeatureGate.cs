namespace QuantamAnalytics.Infrastructure.Features;

/// <summary>
/// Boundary for runtime feature flags. Today the implementation reads from
/// the standard <c>IConfiguration</c> tree (<c>Features:&lt;name&gt;</c>) so flags can
/// flip per-environment via <c>appsettings</c>, env vars, or Azure App
/// Configuration without a deploy. Tomorrow it can be swapped for a
/// vendor (LaunchDarkly, GrowthBook) without touching call sites.
/// </summary>
/// <remarks>
/// Tenant-aware overrides are intentionally absent in this baseline — they
/// land when the first feature actually needs per-tenant rollout. The
/// abstraction stays simple until a real consumer asks for more.
/// </remarks>
public interface IFeatureGate
{
    /// <summary>
    /// True when <paramref name="name"/> is enabled in the current
    /// environment. Unknown flags return <paramref name="fallback"/>
    /// rather than throwing — a typo in a flag name should not crash
    /// the request.
    /// </summary>
    bool IsEnabled(string name, bool fallback = false);
}
