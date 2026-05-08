using QuantamAnalytics.Infrastructure.JobBoards;

namespace QuantamAnalytics.Tests;

public sealed class StubDicePostingClientTests
{
    [Fact]
    public void Submit_returns_dp_prefixed_id_with_22_hex_chars()
    {
        var client = new StubDicePostingClient();

        var result = client.Submit(NewRequest(Guid.CreateVersion7()));

        result.PostingId.Should().StartWith("dp_");
        result.PostingId.Length.Should().Be("dp_".Length + 22);
        result.PostingUrl.Should().StartWith("https://dice.scaffold.quantamanalitics.com/postings/");
        result.PostingUrl.Should().EndWith(result.PostingId);
    }

    [Fact]
    public void Submit_is_deterministic_for_the_same_job_id()
    {
        var client = new StubDicePostingClient();
        var jobId = Guid.CreateVersion7();

        var first = client.Submit(NewRequest(jobId));
        var second = client.Submit(NewRequest(jobId));

        first.PostingId.Should().Be(second.PostingId);
        first.PostingUrl.Should().Be(second.PostingUrl);
    }

    [Fact]
    public void Submit_yields_distinct_ids_for_different_job_ids()
    {
        var client = new StubDicePostingClient();

        var first = client.Submit(NewRequest(Guid.CreateVersion7()));
        var second = client.Submit(NewRequest(Guid.CreateVersion7()));

        first.PostingId.Should().NotBe(second.PostingId);
    }

    [Fact]
    public void Submit_throws_on_empty_job_id()
    {
        var client = new StubDicePostingClient();

        var act = () => client.Submit(NewRequest(Guid.Empty));

        act.Should().Throw<ArgumentException>();
    }

    private static DicePostingRequest NewRequest(Guid jobId) => new(
        TenantId: Guid.CreateVersion7(),
        JobId: jobId,
        TenantName: "Acme Staffing",
        JobTitle: "Senior Backend Engineer",
        JobSlug: "senior-backend-engineer",
        JobLocation: "Remote",
        JobDescription: "Long description.");
}
