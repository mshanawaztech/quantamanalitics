using QuantamAnalytics.Infrastructure.AI;

namespace QuantamAnalytics.Tests;

public sealed class GroqCopilotProviderTests
{
    // Hoisted to a static readonly field to satisfy CA1861 (no constant
    // array arguments at call sites).
    private static readonly string[] SampleSkills = ["C#", "Azure", "EF Core"];

    [Fact]
    public async Task Summarize_falls_back_to_stub_when_api_fails()
    {
        var stub = new StubCopilotProvider();
        var groq = new GroqCopilotProvider(FailingClient(), "test-model", stub);
        var request = new SummarizeCandidateRequest(
            "Jane Candidate", "Cloud engineer", "Years of Azure work.", SampleSkills);

        var viaGroq = await groq.SummarizeCandidateAsync(request, CancellationToken.None);
        var viaStub = await stub.SummarizeCandidateAsync(request, CancellationToken.None);

        // Groq call throws → provider must return the deterministic stub output.
        viaGroq.Summary.Should().Be(viaStub.Summary);
        viaGroq.Highlights.Should().BeEquivalentTo(viaStub.Highlights);
    }

    [Fact]
    public async Task Interview_questions_fall_back_to_stub_when_api_fails()
    {
        var stub = new StubCopilotProvider();
        var groq = new GroqCopilotProvider(FailingClient(), "test-model", stub);
        var request = new ProposeInterviewQuestionsRequest("Senior .NET Engineer", "Build APIs.", 5);

        var viaGroq = await groq.ProposeInterviewQuestionsAsync(request, CancellationToken.None);

        viaGroq.Questions.Should().NotBeEmpty();
    }

    private static HttpClient FailingClient() =>
        new(new FailingHandler()) { BaseAddress = new Uri("https://groq.test/") };

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("simulated Groq outage");
    }
}
