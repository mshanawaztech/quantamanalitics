using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Api.Auth;

/// <summary>
/// Stable permission vocabulary derived from role claims. The frontend
/// consumes these strings via <c>GET /me</c> so portal gating and access
/// labels can stay consistent with backend authorization policy choices.
/// </summary>
public static class PlatformPermissions
{
    public const string RecruitingWorkspace = "recruiting.workspace";
    public const string InterviewWorkspace = "interviews.workspace";
    public const string TimeApproval = "time.approvals";
    public const string PayrollBilling = "payroll.billing";
    public const string ClientPortal = "client.portal";
    public const string CandidatePortal = "candidate.portal";
    public const string PlatformAdmin = "platform.admin";

    public static string[] Expand(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in roles.Distinct(StringComparer.Ordinal))
        {
            switch (role)
            {
                case Roles.PlatformAdmin:
                    permissions.Add(RecruitingWorkspace);
                    permissions.Add(InterviewWorkspace);
                    permissions.Add(TimeApproval);
                    permissions.Add(PayrollBilling);
                    permissions.Add(ClientPortal);
                    permissions.Add(CandidatePortal);
                    permissions.Add(PlatformAdmin);
                    break;

                case Roles.Recruiter:
                    permissions.Add(RecruitingWorkspace);
                    permissions.Add(InterviewWorkspace);
                    break;

                case Roles.HrAdmin:
                    permissions.Add(RecruitingWorkspace);
                    permissions.Add(InterviewWorkspace);
                    break;

                case Roles.Interviewer:
                    permissions.Add(InterviewWorkspace);
                    break;

                case Roles.PayrollAdmin:
                    permissions.Add(TimeApproval);
                    permissions.Add(PayrollBilling);
                    break;

                case Roles.Manager:
                    permissions.Add(RecruitingWorkspace);
                    permissions.Add(InterviewWorkspace);
                    permissions.Add(TimeApproval);
                    permissions.Add(PayrollBilling);
                    break;

                case Roles.Client:
                    permissions.Add(TimeApproval);
                    permissions.Add(ClientPortal);
                    break;

                case Roles.Candidate:
                    permissions.Add(CandidatePortal);
                    break;
            }
        }

        return permissions.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
}
