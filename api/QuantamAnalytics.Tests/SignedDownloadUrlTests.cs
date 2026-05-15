using FluentAssertions;
using QuantamAnalytics.Infrastructure.Storage;

namespace QuantamAnalytics.Tests;

/// <summary>
/// Boundary tests for the signed-download surface introduced in PR-62.
/// We cap TTL at 1 hour by contract; verify rejection at both ends, and
/// verify the disabled fallback returns null (caller's "no link available"
/// branch) instead of throwing.
///
/// The successful-mint path is exercised end-to-end in integration tests
/// against the real AWSSDK pre-signer (no network call — it's all in
/// process). Mocking IAmazonS3 here would require either a mocking library
/// the test project doesn't reference or a 90-method NotImplementedException
/// fake — neither is worth it for an argument-validation contract.
/// </summary>
public sealed class SignedDownloadUrlTests
{
    [Fact]
    public async Task DisabledStorage_returns_null_signed_link()
    {
        var storage = new DisabledResumeStorage();

        var signed = await storage.CreateSignedDownloadUrlAsync(
            "any/key",
            TimeSpan.FromMinutes(5),
            "candidate-resume.pdf",
            CancellationToken.None);

        signed.Should().BeNull();
    }

    [Fact]
    public async Task R2Storage_rejects_zero_ttl()
    {
        // The ArgumentOutOfRangeException is raised before the S3 client
        // is touched, so a null! reference is safe for this contract test.
        var storage = new R2ResumeStorage(null!, "bucket");

        var act = async () => await storage.CreateSignedDownloadUrlAsync(
            "any/key", TimeSpan.Zero, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task R2Storage_rejects_ttl_beyond_one_hour()
    {
        var storage = new R2ResumeStorage(null!, "bucket");

        var act = async () => await storage.CreateSignedDownloadUrlAsync(
            "any/key", TimeSpan.FromHours(2), null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task R2Storage_rejects_blank_object_key()
    {
        var storage = new R2ResumeStorage(null!, "bucket");

        var act = async () => await storage.CreateSignedDownloadUrlAsync(
            "   ", TimeSpan.FromMinutes(5), null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
