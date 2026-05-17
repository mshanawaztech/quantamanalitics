namespace QuantamAnalytics.Infrastructure.AI;

/// <summary>
/// Deterministic stub copilot. Produces stable, sensible output
/// keyed off the inputs so the recruiter UX can be exercised end-to-end
/// before any tenant pays for OpenAI / Anthropic credits.
///
/// "Deterministic" matters for two reasons:
///   1. Demo / regression tests don't drift between runs.
///   2. The same candidate or job always produces the same suggestion
///      list, which means a recruiter walkthrough doesn't surface
///      different bullet copy on each render.
///
/// Real implementations swap in via DI without callers changing.
/// </summary>
public sealed class StubCopilotProvider : ICopilotProvider
{
    public Task<CopilotCandidateSummary> SummarizeCandidateAsync(
        SummarizeCandidateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = string.IsNullOrWhiteSpace(request.CandidateName) ? "the candidate" : request.CandidateName.Trim();
        var headline = string.IsNullOrWhiteSpace(request.Headline) ? "an experienced professional" : request.Headline.Trim();
        var topSkills = request.Skills.Take(3).ToArray();

        var summary = topSkills.Length > 0
            ? $"{name} is {headline}, with hands-on experience across {Join(topSkills)}."
            : $"{name} is {headline}.";

        var highlights = new List<string>(4)
        {
            $"Self-described role: {headline}.",
        };
        if (topSkills.Length > 0)
        {
            highlights.Add($"Strongest declared skills: {Join(topSkills)}.");
        }
        if (!string.IsNullOrWhiteSpace(request.Summary))
        {
            var first = FirstSentence(request.Summary);
            if (!string.IsNullOrWhiteSpace(first))
            {
                highlights.Add(first);
            }
        }
        highlights.Add("Review the resume and recent timeline activity before scheduling.");

        return Task.FromResult(new CopilotCandidateSummary(summary, highlights));
    }

    public Task<CopilotInterviewQuestions> ProposeInterviewQuestionsAsync(
        ProposeInterviewQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var n = Math.Clamp(request.Count <= 0 ? 5 : request.Count, 3, 12);
        var jobLabel = string.IsNullOrWhiteSpace(request.JobTitle) ? "this role" : request.JobTitle.Trim();

        var bank = new[]
        {
            $"Walk me through a recent project you led that's most relevant to {jobLabel}.",
            "Tell me about a time you had to ship something incomplete to hit a deadline — what did you cut, and why?",
            "Describe how you approach onboarding into a new codebase.",
            "What's a piece of feedback you got that genuinely changed how you work?",
            "Talk about a system you designed end to end. What would you do differently if you started over?",
            "When have you had to push back on a stakeholder's plan? How did you frame it?",
            "Tell me about an outage or incident you owned. What did the postmortem surface?",
            "Describe a time you mentored someone who was struggling.",
            "What does 'good enough to ship' mean to you? Give me a concrete example.",
            "How do you decide when to write a test before the code vs. after?",
            "What's a tool or workflow you adopted in the last year that paid off?",
            "How do you handle disagreement on technical direction with a peer?",
        };

        return Task.FromResult(new CopilotInterviewQuestions(bank.Take(n).ToArray()));
    }

    public Task<CopilotOnboardingChecklist> ProposeOnboardingChecklistAsync(
        ProposeOnboardingChecklistRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var role = string.IsNullOrWhiteSpace(request.RoleTitle) ? "new hire" : request.RoleTitle.Trim();
        var start = string.IsNullOrWhiteSpace(request.StartDateLabel) ? "Day 1" : request.StartDateLabel.Trim();

        var items = new List<string>
        {
            $"{start}: Send welcome packet and hardware shipment confirmation to the {role}.",
            "Schedule a 30-minute intro with their hiring manager.",
            "Provision Auth0 account + role assignment in the recruiter or contractor portal.",
            "Add to the team's recurring stand-up and onboarding Slack channel.",
            "Share the company handbook, code of conduct, and benefits enrollment link.",
            "Assign an onboarding buddy from the same function.",
            "Schedule a 30-day pulse check.",
            "Schedule a 90-day review.",
        };

        return Task.FromResult(new CopilotOnboardingChecklist(items));
    }

    // CA1859: private helper is only called with arrays — use the concrete
    // type so the JIT can skip the interface dispatch.
    private static string Join(string[] items) =>
        items.Length switch
        {
            0 => string.Empty,
            1 => items[0],
            2 => $"{items[0]} and {items[1]}",
            _ => $"{string.Join(", ", items.Take(items.Length - 1))}, and {items[^1]}",
        };

    private static string FirstSentence(string text)
    {
        var period = text.IndexOf('.', StringComparison.Ordinal);
        if (period < 0)
        {
            return text.Length > 120 ? text[..120].Trim() + "…" : text.Trim();
        }
        var sentence = text[..(period + 1)].Trim();
        return sentence.Length > 200 ? sentence[..200].Trim() + "…" : sentence;
    }
}
