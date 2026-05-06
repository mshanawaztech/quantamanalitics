using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class TimesheetTests
{
    [Fact]
    public void Ctor_normalizes_week_start_and_assigns_defaults()
    {
        var timesheet = new Timesheet(
            Guid.CreateVersion7(),
            "auth0|contractor-1",
            "CONTRACTOR@example.com",
            new DateOnly(2026, 5, 7));

        timesheet.Id.Should().NotBe(Guid.Empty);
        timesheet.Id.Version.Should().Be(7);
        timesheet.ContractorAuthSubject.Should().Be("auth0|contractor-1");
        timesheet.ContractorEmail.Should().Be("contractor@example.com");
        timesheet.WeekStartUtc.Should().Be(new DateOnly(2026, 5, 4));
        timesheet.Status.Should().Be(TimesheetStatus.Draft);
        timesheet.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void AddOrUpdateEntry_creates_and_updates_matching_entry()
    {
        var timesheet = NewTimesheet();

        timesheet.AddOrUpdateEntry(
            new DateOnly(2026, 5, 4),
            8,
            TimeEntryType.Work,
            "Initial shift");

        timesheet.AddOrUpdateEntry(
            new DateOnly(2026, 5, 4),
            7.5m,
            TimeEntryType.Work,
            "Adjusted shift");

        timesheet.Entries.Should().ContainSingle();
        var entry = timesheet.Entries.Single();
        entry.Hours.Should().Be(7.5m);
        entry.Notes.Should().Be("Adjusted shift");
    }

    [Fact]
    public void AddOrUpdateEntry_rejects_dates_outside_the_week()
    {
        var timesheet = NewTimesheet();

        var act = () => timesheet.AddOrUpdateEntry(
            new DateOnly(2026, 5, 12),
            8,
            TimeEntryType.Work,
            null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*within the timesheet week*");
    }

    [Fact]
    public void Submit_requires_entries_and_allows_rejected_resubmission()
    {
        var timesheet = NewTimesheet();
        var emptySubmit = timesheet.Submit;

        emptySubmit.Should().Throw<InvalidOperationException>();

        timesheet.AddOrUpdateEntry(new DateOnly(2026, 5, 4), 8, TimeEntryType.Work, null);
        timesheet.Submit();
        timesheet.Status.Should().Be(TimesheetStatus.Submitted);

        timesheet.Reject("auth0|reviewer-1", "Need corrections");
        timesheet.Status.Should().Be(TimesheetStatus.Rejected);

        timesheet.AddOrUpdateEntry(new DateOnly(2026, 5, 5), 4, TimeEntryType.PaidTimeOff, "PTO");
        timesheet.Submit();
        timesheet.Status.Should().Be(TimesheetStatus.Submitted);
        timesheet.ReviewNote.Should().BeNull();
    }

    [Fact]
    public void Approve_and_reject_only_work_from_submitted_state()
    {
        var timesheet = NewTimesheet();
        timesheet.AddOrUpdateEntry(new DateOnly(2026, 5, 4), 8, TimeEntryType.Work, null);
        timesheet.Submit();

        timesheet.Approve("auth0|reviewer-1", "Approved");
        timesheet.Status.Should().Be(TimesheetStatus.Approved);
        timesheet.ReviewedByAuthSubject.Should().Be("auth0|reviewer-1");
        timesheet.ReviewNote.Should().Be("Approved");

        var rejectApproved = () => timesheet.Reject("auth0|reviewer-1", "Late");
        rejectApproved.Should().Throw<InvalidOperationException>();
    }

    private static Timesheet NewTimesheet() =>
        new(
            Guid.CreateVersion7(),
            "auth0|contractor-1",
            "contractor@example.com",
            new DateOnly(2026, 5, 4));
}
