using System.Globalization;

namespace QuantamAnalytics.Infrastructure.JobBoards;

/// <summary>
/// Deterministic Dice posting stub. Same input → same posting id, every
/// time, so retries are idempotent and tests can pin expected values.
/// </summary>
/// <remarks>
/// Posting id format: <c>dp_</c> + first 22 hex chars of the job id with
/// dashes stripped. Posting URL points at <c>dice.scaffold.quantamanalitics.com</c>
/// — clearly not a real Dice host, by design. When the real partner client
/// lands in Phase 5 the consumers (recruiter UI, future audit log) keep
/// the same return shape, only the host changes.
/// </remarks>
public sealed class StubDicePostingClient : IDicePostingClient
{
    public DicePostingResult Submit(DicePostingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.JobId == Guid.Empty)
        {
            throw new ArgumentException(
                "JobId must be non-empty. Empty Guid would yield a non-unique posting id.",
                nameof(request));
        }

        // 22 hex chars is enough that two random Guid v7s won't collide
        // within the lifetime of the stub. Strip dashes so the id is one
        // contiguous token a recruiter could paste without re-typing.
        var posting = "dp_" + request.JobId.ToString("N", CultureInfo.InvariantCulture)[..22];
        var url = $"https://dice.scaffold.quantamanalitics.com/postings/{posting}";

        return new DicePostingResult(posting, url);
    }
}
