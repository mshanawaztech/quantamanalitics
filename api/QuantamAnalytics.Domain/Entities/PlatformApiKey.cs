using System.Security.Cryptography;
using System.Text;
using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Phase 9 / Story 70 — API key issued to a tenant for the public
/// platform API. Only the SHA-256 hash is stored; the plaintext is
/// shown once at create time and never recoverable.
///
/// Key format: <c>qa_live_&lt;32 base32url chars&gt;</c>. The prefix lets
/// users grep their .env files when rotating credentials; the suffix is
/// 160 bits of entropy.
/// </summary>
public sealed class PlatformApiKey : ITenantScoped
{
    private PlatformApiKey() { }

    private PlatformApiKey(
        Guid tenantId,
        string label,
        string keyHash,
        string keyPrefix,
        string createdByAuthSubject)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Label = label;
        KeyHash = keyHash;
        KeyPrefix = keyPrefix;
        CreatedByAuthSubject = createdByAuthSubject;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        IsActive = true;
    }

    /// <summary>
    /// Mint a fresh key. Returns the new aggregate plus the one-time
    /// plaintext the caller must surface to the user immediately and
    /// never persist anywhere.
    /// </summary>
    public static (PlatformApiKey Entity, string PlaintextKey) Issue(
        Guid tenantId,
        string label,
        string createdByAuthSubject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByAuthSubject);

        var entropy = RandomNumberGenerator.GetBytes(20);
        var suffix = Base32UrlEncode(entropy);
        var plaintext = $"qa_live_{suffix}";
        var hash = HashKey(plaintext);
        // First 12 chars (after the qa_live_ prefix) make the key
        // identifiable in the UI without revealing the secret.
        var displayPrefix = plaintext[..12];

        return (
            new PlatformApiKey(tenantId, label.Trim(), hash, displayPrefix, createdByAuthSubject.Trim()),
            plaintext);
    }

    public static string HashKey(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Label { get; private set; } = default!;
    public string KeyHash { get; private set; } = default!;
    public string KeyPrefix { get; private set; } = default!;
    public string CreatedByAuthSubject { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastUsedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    public void Revoke() => IsActive = false;

    public void RecordUse(DateTimeOffset whenUtc)
    {
        // Skip writes that wouldn't move the needle — this lets us call
        // RecordUse on every request without pounding the DB. Calls more
        // than 60 seconds apart still update.
        if (LastUsedAtUtc is { } last && (whenUtc - last) < TimeSpan.FromSeconds(60))
        {
            return;
        }
        LastUsedAtUtc = whenUtc;
    }

    private static string Base32UrlEncode(byte[] bytes)
    {
        // RFC 4648 base32 without padding — friendlier than base64 in
        // URLs and env files; case-insensitive on the way in.
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder(bytes.Length * 8 / 5 + 1);
        var buffer = 0;
        var bits = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                sb.Append(Alphabet[(buffer >> bits) & 0x1F]);
            }
        }
        if (bits > 0)
        {
            sb.Append(Alphabet[(buffer << (5 - bits)) & 0x1F]);
        }
        return sb.ToString();
    }
}
