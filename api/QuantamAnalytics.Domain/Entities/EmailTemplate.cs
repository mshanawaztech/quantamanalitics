using System.Text.RegularExpressions;
using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Recruiter-authored email template scoped to a single tenant. Used as the
/// source-of-truth for outbound recruiter messaging (interview invites,
/// rejection notes, follow-ups) so a tenant can edit copy without code
/// changes and so the audit log can attribute every send back to the
/// template that produced it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Slug"/> is the stable, lowercase-kebab handle (e.g.
/// <c>interview-invite</c>) referenced from code that picks a template by
/// well-known name. It is unique per tenant — two tenants may both own a
/// template called <c>interview-invite</c>, but the same tenant cannot.
/// </para>
/// <para>
/// <see cref="Name"/> is the human-readable label shown in the recruiter
/// UI. It is free-form and may change without breaking lookups (which key
/// off the slug).
/// </para>
/// </remarks>
public sealed partial class EmailTemplate : ITenantScoped
{
    // Slug shape — lowercase ascii letters, digits, and single dashes between
    // segments. No leading / trailing / consecutive dashes. Source-generated
    // so the analyzer doesn't nag about runtime Regex construction (SYSLIB1045)
    // and the pattern is compiled once at startup, not per-call.
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    // EF Core needs a parameterless ctor for materialization. Keep it private
    // so callers must go through the public ctor with the required fields.
    private EmailTemplate() { }

    public EmailTemplate(
        Guid tenantId,
        string slug,
        string name,
        string subject,
        string bodyMarkdown,
        string createdByAuthSubject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyMarkdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByAuthSubject);

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (!SlugPattern().IsMatch(normalizedSlug))
        {
            throw new ArgumentException(
                "Slug must be lowercase kebab-case (letters, digits, single dashes between segments).",
                nameof(slug));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Slug = normalizedSlug;
        Name = name.Trim();
        Subject = subject.Trim();
        BodyMarkdown = bodyMarkdown.Trim();
        CreatedByAuthSubject = createdByAuthSubject.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Stable lowercase-kebab handle. Unique per tenant.</summary>
    public string Slug { get; private set; } = default!;

    /// <summary>Human-readable label shown in the recruiter UI.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Subject line used as-is when the template renders.</summary>
    public string Subject { get; private set; } = default!;

    /// <summary>
    /// Markdown body of the email. Stored as <c>text</c> in Postgres —
    /// templates can run long (multi-paragraph onboarding notes etc.) and
    /// arbitrary length caps cause more support tickets than they save.
    /// </summary>
    public string BodyMarkdown { get; private set; } = default!;

    /// <summary>Auth0 subject of the recruiter who created the template.</summary>
    public string CreatedByAuthSubject { get; private set; } = default!;

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Update the editable fields and bump <see cref="UpdatedAtUtc"/>.
    /// Slug is intentionally not editable here — callers that want a new
    /// slug should create a new template, since the slug is the stable
    /// reference other code uses to look the template up.
    /// </summary>
    public void UpdateContent(string name, string subject, string bodyMarkdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyMarkdown);

        Name = name.Trim();
        Subject = subject.Trim();
        BodyMarkdown = bodyMarkdown.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
