namespace QuantamAnalytics.Infrastructure.JobBoards;

/// <summary>
/// Provider boundary for posting jobs to Dice. Phase 4 ships a deterministic
/// stub that mints a fake <c>dp_</c>-prefixed posting id + URL; Phase 5
/// swaps this for the real Dice partner API once a contract is in place.
/// </summary>
/// <remarks>
/// No DB persistence today. The recruiter receives the posting URL in the
/// HTTP response and is responsible for capturing it externally if they
/// need to track it. Persistence + state model land alongside the real
/// Dice integration in Phase 5 — same shape Checkr / DocuSeal followed.
/// </remarks>
public interface IDicePostingClient
{
    DicePostingResult Submit(DicePostingRequest request);
}

public sealed record DicePostingRequest(
    Guid TenantId,
    Guid JobId,
    string TenantName,
    string JobTitle,
    string JobSlug,
    string JobLocation,
    string JobDescription);

public sealed record DicePostingResult(
    string PostingId,
    string PostingUrl);
