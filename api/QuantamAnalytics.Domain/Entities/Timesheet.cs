using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Weekly contractor timesheet owned by a tenant. Serves as the approval and
/// invoice-ready aggregate for later Phase 2 workflows.
/// </summary>
public sealed class Timesheet : ITenantScoped
{
    private readonly List<TimeEntry> _entries = [];

    private Timesheet() { }

    public Timesheet(
        Guid tenantId,
        string contractorAuthSubject,
        string contractorEmail,
        DateOnly weekStartUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractorAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractorEmail);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ContractorAuthSubject = contractorAuthSubject.Trim();
        ContractorEmail = contractorEmail.Trim().ToLowerInvariant();
        WeekStartUtc = NormalizeWeekStart(weekStartUtc);
        Status = TimesheetStatus.Draft;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ContractorAuthSubject { get; private set; } = default!;
    public string ContractorEmail { get; private set; } = default!;
    public DateOnly WeekStartUtc { get; private set; }
    public TimesheetStatus Status { get; private set; }
    public string? ReviewedByAuthSubject { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<TimeEntry> Entries => _entries;

    public void AddOrUpdateEntry(
        DateOnly workDate,
        decimal hours,
        TimeEntryType entryType,
        string? notes)
    {
        EnsureEditable();
        EnsureDateWithinWeek(workDate);

        var existingEntry = _entries.SingleOrDefault(x =>
            x.WorkDate == workDate && x.EntryType == entryType);

        if (existingEntry is null)
        {
            _entries.Add(new TimeEntry(
                TenantId,
                Id,
                workDate,
                hours,
                entryType,
                notes));
        }
        else
        {
            existingEntry.Update(hours, notes);
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void RemoveEntry(DateOnly workDate, TimeEntryType entryType)
    {
        EnsureEditable();

        var existingEntry = _entries.SingleOrDefault(x =>
            x.WorkDate == workDate && x.EntryType == entryType);

        if (existingEntry is null)
        {
            return;
        }

        _entries.Remove(existingEntry);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Submit()
    {
        if (_entries.Count == 0)
        {
            throw new InvalidOperationException("Timesheet must contain at least one entry before submission.");
        }

        if (Status is not (TimesheetStatus.Draft or TimesheetStatus.Rejected))
        {
            throw new InvalidOperationException($"Cannot submit a timesheet in {Status} status.");
        }

        Status = TimesheetStatus.Submitted;
        SubmittedAtUtc = DateTimeOffset.UtcNow;
        ReviewedAtUtc = null;
        ReviewedByAuthSubject = null;
        ReviewNote = null;
        UpdatedAtUtc = SubmittedAtUtc.Value;
    }

    public void Approve(string reviewedByAuthSubject, string? reviewNote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewedByAuthSubject);

        if (Status != TimesheetStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted timesheets can be approved.");
        }

        Status = TimesheetStatus.Approved;
        ReviewedByAuthSubject = reviewedByAuthSubject.Trim();
        ReviewNote = Normalize(reviewNote);
        ReviewedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = ReviewedAtUtc.Value;
    }

    public void Reject(string reviewedByAuthSubject, string reviewNote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewedByAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewNote);

        if (Status != TimesheetStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted timesheets can be rejected.");
        }

        Status = TimesheetStatus.Rejected;
        ReviewedByAuthSubject = reviewedByAuthSubject.Trim();
        ReviewNote = reviewNote.Trim();
        ReviewedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = ReviewedAtUtc.Value;
    }

    private void EnsureEditable()
    {
        if (Status is not (TimesheetStatus.Draft or TimesheetStatus.Rejected))
        {
            throw new InvalidOperationException($"Timesheet entries cannot be changed in {Status} status.");
        }
    }

    private void EnsureDateWithinWeek(DateOnly workDate)
    {
        var weekEnd = WeekStartUtc.AddDays(6);
        if (workDate < WeekStartUtc || workDate > weekEnd)
        {
            throw new InvalidOperationException("Time entries must fall within the timesheet week.");
        }
    }

    private static DateOnly NormalizeWeekStart(DateOnly value)
    {
        var offset = ((int)value.DayOfWeek + 6) % 7;
        return value.AddDays(-offset);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum TimesheetStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
}
