using System.Net.Http.Json;
using System.Text.Json;

namespace QuantamAnalytics.Infrastructure.AI;

/// <summary>
/// Real candidate-job matcher backed by Groq's OpenAI-compatible API.
/// Activated only when a Groq API key is configured; otherwise DI keeps the
/// deterministic <see cref="StubCandidateMatcher"/>. Falls back to the stub
/// on ANY failure (network, auth, malformed JSON) so enabling AI can never
/// break the recruiter scorecard — worst case it degrades to the heuristic
/// score.
/// </summary>
public sealed class GroqCandidateMatcher : ICandidateMatcher
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ICandidateMatcher _fallback;

    public GroqCandidateMatcher(HttpClient http, string model, ICandidateMatcher fallback)
    {
        _http = http;
        _model = model;
        _fallback = fallback;
    }

    public async Task<CandidateMatchScore> ScoreAsync(
        CandidateMatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var user =
                $"Candidate headline: {request.CandidateHeadline}\n" +
                $"Candidate summary: {request.CandidateSummary}\n" +
                $"Candidate skills: {string.Join(", ", request.CandidateSkills)}\n\n" +
                $"Job title: {request.JobTitle}\n" +
                $"Job description: {request.JobDescription}\n\n" +
                "Score the fit. Return JSON {\"overall\": number 0-1, \"skillsCoverage\": number 0-1, " +
                "\"seniorityFit\": number 0-1, \"matchedSkills\": string[], \"gapSkills\": string[], " +
                "\"summary\": string (1-2 sentences)}.";

            var json = await CompleteJsonAsync(
                "You are a technical recruiting assistant scoring candidate-job fit. Be objective. Reply with JSON only.",
                user, cancellationToken);

            var summary = GetString(json, "summary");
            if (string.IsNullOrWhiteSpace(summary))
            {
                return await _fallback.ScoreAsync(request, cancellationToken);
            }

            return new CandidateMatchScore(
                GetUnitScore(json, "overall"),
                GetUnitScore(json, "skillsCoverage"),
                GetUnitScore(json, "seniorityFit"),
                GetStringArray(json, "matchedSkills"),
                GetStringArray(json, "gapSkills"),
                summary);
        }
        catch (Exception)
        {
            return await _fallback.ScoreAsync(request, cancellationToken);
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

    /// <summary>Reads a 0-1 score, tolerating numbers or numeric strings, and clamps.</summary>
    private static double GetUnitScore(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
        {
            return 0;
        }

        var raw = value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0,
        };
        return Math.Clamp(raw, 0, 1);
    }

    private static List<string> GetStringArray(JsonElement obj, string name)
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
