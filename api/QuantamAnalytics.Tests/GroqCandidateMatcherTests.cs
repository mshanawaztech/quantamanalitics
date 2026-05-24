using QuantamAnalytics.Infrastructure.AI;

namespace QuantamAnalytics.Tests;

public sealed class GroqCandidateMatcherTests
{
    private static readonly string[] CandidateSkills = ["C#", "ASP.NET Core", "PostgreSQL", "Azure"];

    [Fact]
    public async Task Score_falls_back_to_stub_when_api_fails()
    {
        var stub = new StubCandidateMatcher();
        var groq = new GroqCandidateMatcher(FailingClient(), "test-model", stub);
        var request = new CandidateMatchRequest(
            "Senior backend engineer",
            "Builds .NET services on Azure.",
            CandidateSkills,
            "Senior .NET Engineer",
            "We need a senior engineer with C#, ASP.NET Core, and PostgreSQL on Azure.");

        var viaGroq = await groq.ScoreAsync(request, CancellationToken.None);
        var viaStub = await stub.ScoreAsync(request, CancellationToken.None);

        // Groq call throws → must return the deterministic stub scorecard.
        viaGroq.Summary.Should().Be(viaStub.Summary);
        viaGroq.Overall.Should().Be(viaStub.Overall);
        viaGroq.Overall.Should().BeInRange(0, 1);
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
