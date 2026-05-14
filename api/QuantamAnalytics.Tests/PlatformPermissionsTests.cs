using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Tests;

public sealed class PlatformPermissionsTests
{
    [Fact]
    public void Expand_returns_expected_permissions_for_hr_and_interviewer_roles()
    {
        var permissions = PlatformPermissions.Expand(new[] { Roles.HrAdmin, Roles.Interviewer });

        permissions.Should().BeEquivalentTo(new[]
        {
            PlatformPermissions.InterviewWorkspace,
            PlatformPermissions.RecruitingWorkspace,
        });
    }

    [Fact]
    public void Expand_returns_payroll_and_approval_permissions_for_manager()
    {
        var permissions = PlatformPermissions.Expand(new[] { Roles.Manager });

        permissions.Should().BeEquivalentTo(new[]
        {
            PlatformPermissions.InterviewWorkspace,
            PlatformPermissions.PayrollBilling,
            PlatformPermissions.RecruitingWorkspace,
            PlatformPermissions.TimeApproval,
        });
    }
}
