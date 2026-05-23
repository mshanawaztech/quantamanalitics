using System.Net.Http.Json;
using System.Text.Json;

namespace QuantamAnalytics.Infrastructure.AI;

/// <summary>
/// Real copilot backed by Groq's OpenAI-compatible chat-completions API.
/// Activated only when a Groq API key is configured; otherwise DI keeps the
/// deterministic <see cref="StubCopilotProvider"/>. Every call falls back to
/// the stub on ANY failure (network, auth, malformed JSON), so enabling the
/// key can never break a recruiter flow — worst case it degrades to the
/// deterministic output.
/// </summary>
public sealed class GroqCopilotProvider : ICopilotProvider
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ICopilotProvider _fallback;

    public GroqCopilotProvider(HttpClient http, string model, ICopilotProvider fallback)
    {
        _http = http;
        _model = model;
        _fallback = fallback;
    }

    public async Task<CopilotCandidateSummary> SummarizeCandidateAsync(
        SummarizeCandidateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var user =
                $"Candidate: {request.CandidateName}\nHeadline: {request.Headline}\n" +
                $"Skills: {string.Join(", ", request.Skills)}\nSummary: {request.Summary}\n\n" +
                "Return JSON {\"summary\": string (2-3 sentences), \"highlights\": string[] (3-5 bullet points)}.";
            var json = await CompleteJsonAsync(
                "You are a recruiting assistant. Summarize candidates factually and concisely. Reply with JSON only.",
                user, cancellationToken);

            var summary = GetString(json, "summary");
            var highlights = GetStringArray(json, "highlights");
            if (string.IsNullOrWhiteSpace(summary) || highlights.Count == 0)
            {
                return await _fallback.SummarizeCandidateAsync(request, cancellationToken);
            }
            return new CopilotCandidateSummary(summary, highlights);
        }
        catch (Exception)
        {
            return await _fallback.SummarizeCandidateAsync(request, cancellationToken);
        }
    }

    public async Task<CopilotInterviewQuestions> ProposeInterviewQuestionsAsync(
        ProposeInterviewQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var count = Math.Clamp(request.Count <= 0 ? 5 : request.Count, 3, 12);
        try
        {
            var user =
                $"Job title: {request.JobTitle}\nDescription: {request.JobDescription}\n\n" +
                $"Return JSON {{\"questions\": string[] (exactly {count} interview questions tailored to this role)}}.";
            var json = await CompleteJsonAsync(
                "You are an interview-prep assistant for recruiters. Reply with JSON only.",
                user, cancellationToken);

            var questions = GetStringArray(json, "questions");
            if (questions.Count == 0)
            {
                return await _fallback.ProposeInterviewQuestionsAsync(request, cancellationToken);
            }
            return new CopilotInterviewQuestions(questions);
        }
        catch (Exception)
        {
            return await _fallback.ProposeInterviewQuestionsAsync(request, cancellationToken);
        }
    }

    public async Task<CopilotOnboardingChecklist> ProposeOnboardingChecklistAsync(
        ProposeOnboardingChecklistRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var user =
                $"Role: {request.RoleTitle}\nStart: {request.StartDateLabel}\n\n" +
                "Return JSON {\"items\": string[] (5-10 onboarding checklist items for this hire)}.";
            var json = await CompleteJsonAsync(
                "You are an HR onboarding assistant. Reply with JSON only.",
                user, cancellationToken);

            var items = GetStringArray(json, "items");
            if (items.Count == 0)
            {
                return await _fallback.ProposeOnboardingChecklistAsync(request, cancellationToken);
            }
            return new CopilotOnboardingChecklist(items);
        }
        catch (Exception)
        {
            return await _fallback.ProposeOnboardingChecklistAsync(request, cancellationToken);
        }
    }

    private async Task<JsonElement> CompleteJsonAsync(string system, string user, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = _model,
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
        };

        using var response = await _http.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        _ = response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        using var inner = JsonDocument.Parse(content);
        return inner.RootElement.Clone();
    }

    private static string GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static IReadOnlyList<string> GetStringArray(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();
    }
}
