using FluentAssertions;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

/// <summary>
/// Phase 8 / Story 61 — pin the soft-delete contract on the Job aggregate.
/// The recycle-bin endpoint relies on every clause here being true.
/// </summary>
public sealed class JobSoftDeleteTests
{
    [Fact]
    public void New_job_is_not_deleted()
    {
        var job = NewJob();

        job.IsDeleted.Should().BeFalse();
        job.DeletedAtUtc.Should().BeNull();
        job.DeletedByAuthSubject.Should().BeNull();
    }

    [Fact]
    public void SoftDelete_marks_the_record_and_unpublishes_it()
    {
        var job = NewJob();
        var deletedAt = DateTimeOffset.UtcNow;

        job.SoftDelete("auth0|recruiter-1", deletedAt);

        job.IsDeleted.Should().BeTrue();
        job.DeletedAtUtc.Should().Be(deletedAt);
        job.DeletedByAuthSubject.Should().Be("auth0|recruiter-1");
        job.IsPublished.Should().BeFalse(
            "a soft-deleted job must never stay on the public board");
    }

    [Fact]
    public void SoftDelete_is_idempotent()
    {
        var job = NewJob();
        var first = DateTimeOffset.UtcNow.AddMinutes(-1);
        var second = DateTimeOffset.UtcNow;

        job.SoftDelete("auth0|first", first);
        job.SoftDelete("auth0|second", second);

        job.DeletedAtUtc.Should().Be(first,
            "the second call must not overwrite who pulled the trigger or when");
        job.DeletedByAuthSubject.Should().Be("auth0|first");
    }

    [Fact]
    public void SoftDelete_rejects_blank_auth_subject()
    {
        var job = NewJob();

        var act = () => job.SoftDelete(" ", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Restore_inside_window_clears_deletion_metadata()
    {
        var job = NewJob();
        var deletedAt = DateTimeOffset.UtcNow.AddDays(-3);
        job.SoftDelete("auth0|rec", deletedAt);

        job.Restore(deletedAt.AddDays(5), TimeSpan.FromDays(30));

        job.IsDeleted.Should().BeFalse();
        job.DeletedAtUtc.Should().BeNull();
        job.DeletedByAuthSubject.Should().BeNull();
        // IsPublished remains where SoftDelete left it — the restorer
        // makes a deliberate Publish() call if they want it visible again.
        job.IsPublished.Should().BeFalse();
    }

    [Fact]
    public void Restore_past_window_throws()
    {
        var job = NewJob();
        var deletedAt = DateTimeOffset.UtcNow.AddDays(-45);
        job.SoftDelete("auth0|rec", deletedAt);

        var act = () => job.Restore(DateTimeOffset.UtcNow, TimeSpan.FromDays(30));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*restore window*");
    }

    [Fact]
    public void Restore_on_live_record_throws()
    {
        var job = NewJob();

        var act = () => job.Restore(DateTimeOffset.UtcNow, TimeSpan.FromDays(30));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*deleted jobs*");
    }

    private static Job NewJob() =>
        new(
            tenantId: Guid.CreateVersion7(),
            title: "Senior Engineer",
            slug: "senior-engineer",
            location: "Remote",
            summary: "Build the platform.",
            description: "Long description.",
            postedOnUtc: DateOnly.FromDateTime(DateTime.UtcNow.Date));
}
