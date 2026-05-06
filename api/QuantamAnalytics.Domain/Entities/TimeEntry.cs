using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// A dated unit of time within a weekly timesheet. Entry type keeps the domain
/// open for PTO and related pay-rule logic without introducing billing math yet.
/// </summary>
public sealed class TimeEntry : ITenantScoped
{
    private TimeEntry() { }

    internal TimeEntry(
        Guid tenantId,
        Guid timesheetId,
        DateOnly workDate,
        decimal hours,
        TimeEntryType entryType,
        string? notes)
    {
        ValidateHours(hours);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        TimesheetId = timesheetId;
        WorkDate = workDate;
        Hours = hours;
        EntryType = entryType;
        Notes = Normalize(notes);
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TimesheetId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public decimal Hours { get; private set; }
    public TimeEntryType EntryType { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    internal void Update(decimal hours, string? notes)
    {
        ValidateHours(hours);

        Hours = hours;
        Notes = Normalize(notes);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static void ValidateHours(decimal hours)
    {
        if (hours <= 0 || hours > 24)
        {
            throw new ArgumentOutOfRangeException(nameof(hours), "Hours must be greater than 0 and no more than 24.");
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum TimeEntryType
{
    Work = 0,
    PaidTimeOff = 1,
}
