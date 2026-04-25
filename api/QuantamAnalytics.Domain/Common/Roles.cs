namespace QuantamAnalytics.Domain.Common;

/// <summary>
/// Application roles. The string values must match exactly what comes back
/// from Auth0's custom-claims action — see <c>docs/auth.md</c>.
///
/// Roles in Phase 1: PlatformAdmin (Quantam staff), Recruiter (staffing-firm
/// employee), Candidate (job seeker). Client + Contractor land in later phases.
/// </summary>
public static class Roles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string Recruiter = "Recruiter";
    public const string Candidate = "Candidate";

    /// <summary>All roles known to the platform. Useful for validation.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PlatformAdmin,
        Recruiter,
        Candidate,
    };

    /// <summary>
    /// Custom claim namespace Auth0 emits roles + tenant_id under. Anything
    /// else (e.g. claims emitted under a non-namespaced key) gets dropped by
    /// Auth0 silently — namespacing is mandatory.
    /// </summary>
    public const string ClaimNamespace = "https://quantamanalitics.com/";

    public const string RolesClaim = ClaimNamespace + "roles";
    public const string TenantIdClaim = ClaimNamespace + "tenant_id";
}
