using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Esign;

namespace QuantamAnalytics.Tests;

public sealed class EsignDocumentTests
{
    [Fact]
    public void Ctor_starts_in_Drafted_state()
    {
        var doc = NewDoc();

        doc.Status.Should().Be(EsignDocumentStatus.Drafted);
        doc.SentAtUtc.Should().BeNull();
        doc.ProviderSubmissionId.Should().BeNull();
        doc.SigningUrl.Should().BeNull();
    }

    [Fact]
    public void AttachProviderSubmission_moves_state_to_Sent()
    {
        var doc = NewDoc();

        doc.AttachProviderSubmission("sub_abc", "https://docuseal.example/sign/abc");

        doc.Status.Should().Be(EsignDocumentStatus.Sent);
        doc.ProviderSubmissionId.Should().Be("sub_abc");
        doc.SigningUrl.Should().Be("https://docuseal.example/sign/abc");
        doc.SentAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void AttachProviderSubmission_is_idempotent()
    {
        var doc = NewDoc();
        doc.AttachProviderSubmission("sub_abc", "https://docuseal.example/sign/abc");
        var firstUpdate = doc.UpdatedAtUtc;

        doc.AttachProviderSubmission("sub_abc", "https://docuseal.example/sign/abc");

        doc.UpdatedAtUtc.Should().Be(firstUpdate);
    }

    [Fact]
    public void MarkViewed_does_not_regress_after_signed()
    {
        var doc = NewDoc();
        doc.AttachProviderSubmission("sub_abc", "https://docuseal.example/sign/abc");
        doc.MarkSigned();

        doc.MarkViewed();

        doc.Status.Should().Be(EsignDocumentStatus.Signed);
    }

    [Fact]
    public void MarkSigned_sets_terminal_signed_status()
    {
        var doc = NewDoc();
        doc.AttachProviderSubmission("sub_abc", "https://docuseal.example/sign/abc");

        doc.MarkSigned();

        doc.Status.Should().Be(EsignDocumentStatus.Signed);
        doc.SignedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_throws_after_signed()
    {
        var doc = NewDoc();
        doc.AttachProviderSubmission("sub_abc", "https://docuseal.example/sign/abc");
        doc.MarkSigned();

        var act = () => doc.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task StubDocuSealClient_returns_deterministic_submission_and_url()
    {
        var doc = NewDoc();
        var client = new StubDocuSealClient();

        var first = await client.CreateSubmissionAsync(doc, CancellationToken.None);
        var second = await client.CreateSubmissionAsync(doc, CancellationToken.None);

        first.ProviderSubmissionId.Should().StartWith("sub_");
        first.ProviderSubmissionId.Should().Be(second.ProviderSubmissionId);
        first.SigningUrl.Should().Be(second.SigningUrl);
        first.SigningUrl.Should().StartWith("https://esign.scaffold.quantamanalitics.com/sign/");
    }

    private static EsignDocument NewDoc() => new(
        tenantId: Guid.CreateVersion7(),
        candidateProfileId: Guid.CreateVersion7(),
        candidateEmail: "candidate@example.com",
        candidateName: "Candidate Example",
        sentByAuthSubject: "auth0|recruiter",
        kind: EsignDocumentKind.OfferLetter,
        templateSlug: "offer_letter_v1",
        subject: "Your offer letter, Candidate Example");
}
