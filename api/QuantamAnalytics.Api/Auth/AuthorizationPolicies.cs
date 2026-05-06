namespace QuantamAnalytics.Api.Auth;

/// <summary>
/// Policy name constants. Reference these in
/// <c>.RequireAuthorization(AuthorizationPolicies.RequireRecruiter)</c>
/// instead of stringly-typing role names at every call site.
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequirePlatformAdmin = nameof(RequirePlatformAdmin);
    public const string RequireRecruiter = nameof(RequireRecruiter);
    public const string RequireRecruitingAccess = nameof(RequireRecruitingAccess);
    public const string RequireTimeApprovalAccess = nameof(RequireTimeApprovalAccess);
    public const string RequireCandidate = nameof(RequireCandidate);
}
