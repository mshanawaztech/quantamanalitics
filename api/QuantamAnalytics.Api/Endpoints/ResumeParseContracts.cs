namespace QuantamAnalytics.Api.Endpoints;

public sealed record ParsedResumeResponse(
    string? FullName,
    string? Email,
    string? PhoneNumber,
    string? Headline,
    string[] Skills,
    ParsedResumeWorkItem[] WorkHistory);

public sealed record ParsedResumeWorkItem(
    string Employer,
    string Title,
    DateOnly? StartDate,
    DateOnly? EndDate);
