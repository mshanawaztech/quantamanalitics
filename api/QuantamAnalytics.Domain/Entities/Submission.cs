using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Represents the recruiter-to-client handoff for a candidate after the
/// recruiting team decides an application is ready for client review.
/// </summary>
public sealed class Submission : ITenantScoped
{
    private Submission() { }

    public Submission(
        Guid tenantId,
        Guid applicationId,
        Guid jobId,
        Guid candidateProfileId,
        string candidateEmail,
        string candidateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateName);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        JobId = jobId;
        CandidateProfileId = candidateProfileId;
        CandidateEmail = candidateEmail.Trim().ToLowerInvariant();
        CandidateName = candidateName.Trim();
        Status = SubmissionStatus.Draft;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid JobId { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public string CandidateEmail { get; private set; } = default!;
    public string CandidateName { get; private set; } = default!;
    public string? ClientCompanyName { get; private set; }
    public string? PitchSummary { get; private set; }
    public string? SubmittedByAuthSubject { get; private set; }
    public string? ClientDecisionNote { get; private set; }
    public SubmissionStatus Status { get; private set; }
    public DateTimeOffset? SubmittedToClientAtUtc { get; private set; }
    public DateTimeOffset? ClientDecisionAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void SubmitToClient(
        string recruiterAuthSubject,
        string clientCompanyName,
        string? pitchSummary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recruiterAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientCompanyName);

        if (Status != SubmissionStatus.Draft)
        {
            throw new InvalidOperationException("Only draft submissions can be sent to a client.");
        }

        SubmittedByAuthSubject = recruiterAuthSubject.Trim();
        ClientCompanyName = clientCompanyName.Trim();
        PitchSummary = Normalize(pitchSummary);
        Status = SubmissionStatus.SubmittedToClient;
        SubmittedToClientAtUtc = DateTimeOffset.UtcNow;
        ClientDecisionAtUtc = null;
        ClientDecisionNote = null;
        UpdatedAtUtc = SubmittedToClientAtUtc.Value;
    }

    public void MarkClientReviewing()
    {
        if (Status != SubmissionStatus.SubmittedToClient)
        {
            throw new InvalidOperationException("Only submitted submissions can move into client review.");
        }

        Status = SubmissionStatus.ClientReviewing;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void AcceptByClient(string? note)
    {
        if (Status != SubmissionStatus.SubmittedToClient && Status != SubmissionStatus.ClientReviewing)
        {
            throw new InvalidOperationException("Only active client review submissions can be accepted.");
        }

        Status = SubmissionStatus.ClientAccepted;
        ClientDecisionAtUtc = DateTimeOffset.UtcNow;
        ClientDecisionNote = Normalize(note);
        UpdatedAtUtc = ClientDecisionAtUtc.Value;
    }

    public void DeclineByClient(string? note)
    {
        if (Status != SubmissionStatus.SubmittedToClient && Status != SubmissionStatus.ClientReviewing)
        {
            throw new InvalidOperationException("Only active client review submissions can be declined.");
        }

        Status = SubmissionStatus.ClientDeclined;
        ClientDecisionAtUtc = DateTimeOffset.UtcNow;
        ClientDecisionNote = Normalize(note);
        UpdatedAtUtc = ClientDecisionAtUtc.Value;
    }

    public void Withdraw(string? note)
    {
        if (Status == SubmissionStatus.ClientAccepted || Status == SubmissionStatus.Withdrawn)
        {
            throw new InvalidOperationException("Accepted or already-withdrawn submissions cannot be withdrawn.");
        }

        Status = SubmissionStatus.Withdrawn;
        ClientDecisionAtUtc = DateTimeOffset.UtcNow;
        ClientDecisionNote = Normalize(note);
        UpdatedAtUtc = ClientDecisionAtUtc.Value;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum SubmissionStatus
{
    Draft = 0,
    SubmittedToClient = 1,
    ClientReviewing = 2,
    ClientAccepted = 3,
    ClientDeclined = 4,
    Withdrawn = 5,
}
