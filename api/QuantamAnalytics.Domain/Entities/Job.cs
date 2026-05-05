using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Public-facing job opening owned by a tenant. Represents the first business
/// entity surfaced to anonymous users through the job board.
/// </summary>
public sealed class Job : ITenantScoped
{
    private Job() { }

    public Job(
        Guid tenantId,
        string title,
        string slug,
        string location,
        string summary,
        string description,
        DateOnly postedOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Title = title.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Location = location.Trim();
        Summary = summary.Trim();
        Description = description.Trim();
        PostedOnUtc = postedOnUtc;
        IsPublished = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string Location { get; private set; } = default!;
    public string Summary { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public DateOnly PostedOnUtc { get; private set; }
    public bool IsPublished { get; private set; }

    public void UpdateDetails(
        string title,
        string location,
        string summary,
        string description,
        DateOnly postedOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Title = title.Trim();
        Location = location.Trim();
        Summary = summary.Trim();
        Description = description.Trim();
        PostedOnUtc = postedOnUtc;
    }

    public void Publish() => IsPublished = true;
    public void Unpublish() => IsPublished = false;
}
