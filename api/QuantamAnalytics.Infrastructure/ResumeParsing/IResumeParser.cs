namespace QuantamAnalytics.Infrastructure.ResumeParsing;

/// <summary>
/// Boundary for the resume-parsing provider. Phase 7 ships a deterministic
/// stub so the recruiter UX (and the candidate "extract details" flow) can
/// be exercised end-to-end before any tenant has paid for a real parsing
/// vendor (Sovren, Affinda, HireAbility, etc.). Phase 5 / 9 swaps in a real
/// implementation behind the same interface without callers changing.
/// </summary>
public interface IResumeParser
{
    /// <summary>
    /// Parse the resume bytes and return the extracted fields. The parser
    /// is best-effort — any field that can't be confidently extracted is
    /// returned as <c>null</c>, never invented.
    /// </summary>
    Task<ResumeParseResult> ParseAsync(
        ResumeParseRequest request,
        CancellationToken cancellationToken);
}

public sealed record ResumeParseRequest(
    string FileName,
    string ContentType,
    Stream Content);

public sealed record ResumeParseResult(
    string? FullName,
    string? Email,
    string? PhoneNumber,
    string? Headline,
    string[] Skills,
    ResumeWorkHistoryItem[] WorkHistory);

public sealed record ResumeWorkHistoryItem(
    string Employer,
    string Title,
    DateOnly? StartDate,
    DateOnly? EndDate);
