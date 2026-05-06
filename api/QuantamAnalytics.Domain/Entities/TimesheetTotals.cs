namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Baseline pay-rule projection for a single weekly timesheet. This keeps the
/// Phase 2 model open for invoice/payroll work without introducing rate math yet.
/// </summary>
public sealed record TimesheetTotals(
    decimal WorkHours,
    decimal PaidTimeOffHours,
    decimal RegularHours,
    decimal OvertimeHours,
    decimal PayableHours);
