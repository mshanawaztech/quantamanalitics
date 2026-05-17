using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Per-user, per-tenant in-app notification. Persisted so a user signing in
/// from a fresh device still sees what changed while they were away. Writers
/// today: SessionEndpoint (session-revoked, suspicious-login events) plus
/// any future domain-event consumer.
/// </summary>
/// <remarks>
/// We model "read" with a nullable timestamp instead of a boolean so the UI
/// can render "read 2h ago" without a second column. <see cref="TargetUrl"/>
/// is the relative path the bell-icon dropdown deep-links to.
/// </remarks>
public sealed class Notification : ITenantScoped
{
    private Notification() { }

    public Notification(
        Guid tenantId,
        string recipientAuthSubject,
        string kind,
        string title,
        string body,
        string? targetUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        RecipientAuthSubject = recipientAuthSubject.Trim();
        Kind = kind.Trim();
        Title = title.Trim();
        Body = body.Trim();
        TargetUrl = string.IsNullOrWhiteSpace(targetUrl) ? null : targetUrl.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Auth0 subject of the user the notification is for.</summary>
    public string RecipientAuthSubject { get; private set; } = default!;

    /// <summary>
    /// Short machine-readable category: <c>application.stage_changed</c>,
    /// <c>security.session_revoked</c>, etc. Drives icon + tone selection.
    /// </summary>
    public string Kind { get; private set; } = default!;

    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public string? TargetUrl { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }

    public void MarkRead()
    {
        if (ReadAtUtc is null)
        {
            ReadAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkUnread() => ReadAtUtc = null;
}
