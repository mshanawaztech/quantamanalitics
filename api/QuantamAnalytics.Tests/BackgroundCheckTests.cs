using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.BackgroundChecks;

namespace QuantamAnalytics.Tests;

/// <summary>
/// Entity state-machine + Checkr stub determinism. Real DB-backed tests
/// land alongside the Phase 5 HTTP-backed Checkr client.
/// </summary>
public sealed class BackgroundCheckTests
{
    [Fact]
    public void Ctor_starts_in_Requested_state()
    {
        var check = NewCheck();

        check.Status.Should().Be(BackgroundCheckStatus.Requested);
        check.RequestedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        check.CompletedAtUtc.Should().BeNull();
        check.ProviderReportId.Should().BeNull();
    }

    [Fact]
    public void AttachProviderReport_moves_state_to_InProgress()
    {
        var check = NewCheck();

        check.AttachProviderReport("rep_abc");

        check.Status.Should().Be(BackgroundCheckStatus.InProgress);
        check.ProviderReportId.Should().Be("rep_abc");
    }

    [Fact]
    public void AttachProviderReport_is_idempotent()
    {
        var check = NewCheck();
        check.AttachProviderReport("rep_abc");
        var firstUpdate = check.UpdatedAtUtc;

        check.AttachProviderReport("rep_abc");

        check.UpdatedAtUtc.Should().Be(firstUpdate);
    }

    [Theory]
    [InlineData(BackgroundCheckStatus.Clear)]
    [InlineData(BackgroundCheckStatus.Consider)]
    [InlineData(BackgroundCheckStatus.Cancelled)]
    public void RecordTerminalStatus_accepts_terminal_values(BackgroundCheckStatus terminal)
    {
        var check = NewCheck();
        check.AttachProviderReport("rep_abc");

        check.RecordTerminalStatus(terminal, "from-test");

        check.Status.Should().Be(terminal);
        check.StatusDetail.Should().Be("from-test");
        check.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void RecordTerminalStatus_rejects_non_terminal_values()
    {
        var check = NewCheck();

        var act = () => check.RecordTerminalStatus(BackgroundCheckStatus.InProgress, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RecordTerminalStatus_is_a_no_op_after_already_terminal()
    {
        var check = NewCheck();
        check.AttachProviderReport("rep_abc");
        check.RecordTerminalStatus(BackgroundCheckStatus.Clear, "first");

        // A late-arriving "consider" webhook must not silently overwrite
        // the prior clear decision.
        check.RecordTerminalStatus(BackgroundCheckStatus.Consider, "late");

        check.Status.Should().Be(BackgroundCheckStatus.Clear);
        check.StatusDetail.Should().Be("first");
    }

    [Fact]
    public async Task StubCheckrClient_returns_deterministic_provider_id()
    {
        var check = NewCheck();
        var client = new StubCheckrClient();

        var first = await client.RequestReportAsync(check, CancellationToken.None);
        var second = await client.RequestReportAsync(check, CancellationToken.None);

        first.ProviderReportId.Should().StartWith("rep_");
        first.ProviderReportId.Should().Be(second.ProviderReportId);
        first.Status.Should().Be(BackgroundCheckStatus.InProgress);
    }

    private static BackgroundCheck NewCheck() => new(
        tenantId: Guid.CreateVersion7(),
        candidateProfileId: Guid.CreateVersion7(),
        candidateEmail: "candidate@example.com",
        candidateName: "Candidate Example",
        requestedByAuthSubject: "auth0|recruiter",
        packageSlug: "tasker_standard");
}
