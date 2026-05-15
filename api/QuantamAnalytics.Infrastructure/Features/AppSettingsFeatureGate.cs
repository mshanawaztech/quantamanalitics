using Microsoft.Extensions.Configuration;

namespace QuantamAnalytics.Infrastructure.Features;

/// <summary>
/// Default <see cref="IFeatureGate"/> implementation. Reads flag values
/// from the configuration tree under the <c>Features</c> section. Values
/// can come from <c>appsettings.json</c>, environment variables
/// (<c>Features__name=true</c>), Azure App Configuration, or user secrets
/// — anything that registers as an <see cref="IConfiguration"/> source.
/// </summary>
public sealed class AppSettingsFeatureGate : IFeatureGate
{
    private const string Section = "Features";

    private readonly IConfiguration _configuration;

    public AppSettingsFeatureGate(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsEnabled(string name, bool fallback = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Bind <Features:name> → bool. Missing → fallback. Malformed
        // ("yes" / "1" / "no") → fallback rather than ambiguous truthiness.
        var raw = _configuration[$"{Section}:{name}"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return bool.TryParse(raw, out var enabled) ? enabled : fallback;
    }
}
