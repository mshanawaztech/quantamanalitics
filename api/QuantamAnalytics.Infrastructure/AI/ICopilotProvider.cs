namespace QuantamAnalytics.Infrastructure.AI;

/// <summary>
/// Phase 9 / Story 73 — boundary for the AI copilot. Three discrete
/// tasks today (candidate summary, interview questions, onboarding
/// checklist); the surface is intentionally narrow so the model picker
/// can change without the caller graph rippling.
///
/// Implementations must be tenant-blind. The caller is responsible for
/// loading the source data inside the tenant query filter.
/// </summary>
public interface ICopilotProvider
{
    Task<CopilotCandidateSummary> SummarizeCandidateAsync(
        SummarizeCandidateRequest request,
        CancellationToken cancellationToken);

    Task<CopilotInterviewQuestions> ProposeInterviewQuestionsAsync(
        ProposeInterviewQuestionsRequest request,
        CancellationToken cancellationToken);

    Task<CopilotOnboardingChecklist> ProposeOnboardingChecklistAsync(
        ProposeOnboardingChecklistRequest request,
        CancellationToken cancellationToken);
}

public sealed record SummarizeCandidateRequest(
    string CandidateName,
    string Headline,
    string Summary,
    IReadOnlyList<string> Skills);

public sealed record CopilotCandidateSummary(
    string Summary,
    IReadOnlyList<string> Highlights);

public sealed record ProposeInterviewQuestionsRequest(
    string JobTitle,
    string JobDescription,
    int Count);

public sealed record CopilotInterviewQuestions(
    IReadOnlyList<string> Questions);

public sealed record ProposeOnboardingChecklistRequest(
    string RoleTitle,
    string StartDateLabel);

public sealed record CopilotOnboardingChecklist(
    IReadOnlyList<string> Items);
