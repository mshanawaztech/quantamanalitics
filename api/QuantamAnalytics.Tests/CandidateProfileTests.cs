using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class CandidateProfileTests
{
    [Fact]
    public void Ctor_assigns_identity_and_timestamps()
    {
        var tenantId = Guid.CreateVersion7();

        var profile = new CandidateProfile(
            tenantId,
            "auth0|candidate-1",
            "PERSON@example.com",
            "  Jane Candidate  ");

        profile.Id.Should().NotBe(Guid.Empty);
        profile.Id.Version.Should().Be(7);
        profile.TenantId.Should().Be(tenantId);
        profile.AuthSubject.Should().Be("auth0|candidate-1");
        profile.Email.Should().Be("person@example.com");
        profile.FullName.Should().Be("Jane Candidate");
        profile.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        profile.UpdatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void UpdateProfile_normalizes_optional_fields()
    {
        var profile = new CandidateProfile(
            Guid.CreateVersion7(),
            "auth0|candidate-2",
            "person@example.com",
            null);

        profile.UpdateProfile(
            "updated@example.com",
            "  Jane Candidate ",
            "  +1 555 010 9999 ",
            "  Data recruiter ",
            "  Loves pipeline clarity. ");

        profile.Email.Should().Be("updated@example.com");
        profile.FullName.Should().Be("Jane Candidate");
        profile.PhoneNumber.Should().Be("+1 555 010 9999");
        profile.Headline.Should().Be("Data recruiter");
        profile.Summary.Should().Be("Loves pipeline clarity.");
    }

    [Fact]
    public void AttachResume_sets_metadata()
    {
        var profile = new CandidateProfile(
            Guid.CreateVersion7(),
            "auth0|candidate-3",
            "person@example.com",
            null);
        var uploadedAtUtc = DateTimeOffset.UtcNow;

        profile.AttachResume(
            "candidate-resumes/tenant/user/file.pdf",
            "resume.pdf",
            uploadedAtUtc);

        profile.ResumeObjectKey.Should().Be("candidate-resumes/tenant/user/file.pdf");
        profile.ResumeFileName.Should().Be("resume.pdf");
        profile.ResumeUploadedAtUtc.Should().Be(uploadedAtUtc);
        profile.UpdatedAtUtc.Should().Be(uploadedAtUtc);
    }
}
