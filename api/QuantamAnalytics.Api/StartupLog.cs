using Microsoft.Extensions.Logging;

namespace QuantamAnalytics.Api;

/// <summary>
/// Source-generated logging delegates used at startup. Satisfies CA1848 by
/// pre-generating the formatter at compile time instead of allocating a
/// params object[] on every call. New startup warnings/info lines should
/// be added here rather than calling <c>logger.LogXxx(...)</c> directly.
/// </summary>
internal static partial class StartupLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Auth0 is not configured ({DomainKey} / {AudienceKey} missing). " +
                  "Running with authentication DISABLED — every endpoint is public. " +
                  "Set both keys before exposing this deployment to real users.")]
    public static partial void Auth0NotConfigured(
        this ILogger logger,
        string domainKey,
        string audienceKey);
}
