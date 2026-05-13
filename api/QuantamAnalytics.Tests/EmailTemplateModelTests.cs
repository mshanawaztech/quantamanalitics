using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Tests;

/// <summary>
/// Pure model-build coverage for <see cref="EmailTemplate"/>. Mirrors the
/// shape of <see cref="AppDbContextModelTests"/> — no real Postgres needed,
/// just inspect the EF model to confirm column names, indexes, and
/// value-generation flags. Plus a few constructor-level invariants so the
/// validation rules don't quietly drift.
/// </summary>
public sealed class EmailTemplateModelTests
{
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=qa_dev_test;Username=u;Password=p")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new StubCurrentTenant());
    }

    [Fact]
    public void Model_includes_EmailTemplate_entity()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(EmailTemplate));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("email_templates");
    }

    [Fact]
    public void EmailTemplate_uses_snake_case_columns()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(EmailTemplate))!;
        var columnNames = entity.GetProperties()
            .Select(p => p.GetColumnName())
            .ToArray();

        columnNames.Should().Contain([
            "id",
            "tenant_id",
            "slug",
            "name",
            "subject",
            "body_markdown",
            "created_by_auth_subject",
            "created_at_utc",
            "updated_at_utc",
        ]);
    }

    [Fact]
    public void EmailTemplate_id_is_not_db_generated()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(EmailTemplate))!;
        var idProperty = entity.FindProperty(nameof(EmailTemplate.Id))!;

        idProperty.ValueGenerated.Should().Be(ValueGenerated.Never);
    }

    [Fact]
    public void EmailTemplate_has_unique_tenant_slug_index()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(EmailTemplate))!;
        var slugIndex = entity.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(EmailTemplate.TenantId), nameof(EmailTemplate.Slug)]));

        slugIndex.Should().NotBeNull("slug is the recruiter-facing handle — duplicates within a tenant break lookups");
        slugIndex!.IsUnique.Should().BeTrue();
        slugIndex.GetDatabaseName().Should().Be("ix_email_templates_tenant_slug_unique");
    }

    [Fact]
    public void EmailTemplate_ctor_assigns_Guid_v7_id_and_timestamps()
    {
        var template = new EmailTemplate(
            tenantId: Guid.CreateVersion7(),
            slug: "interview-invite",
            name: "Interview Invite",
            subject: "Time to chat?",
            bodyMarkdown: "Hello {{candidate}}, thanks for applying.",
            createdByAuthSubject: "auth0|recruiter-1");

        template.Id.Should().NotBe(Guid.Empty);
        template.Id.Version.Should().Be(7);
        template.Slug.Should().Be("interview-invite");
        template.Name.Should().Be("Interview Invite");
        template.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        template.UpdatedAtUtc.Should().Be(template.CreatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Has Spaces")]
    [InlineData("-leading-dash")]
    [InlineData("trailing-dash-")]
    [InlineData("double--dash")]
    [InlineData("under_score")]
    [InlineData("punctuation!")]
    public void EmailTemplate_ctor_validates_slug_format(string badSlug)
    {
        var act = () => new EmailTemplate(
            tenantId: Guid.CreateVersion7(),
            slug: badSlug,
            name: "Interview Invite",
            subject: "Time to chat?",
            bodyMarkdown: "Body",
            createdByAuthSubject: "auth0|recruiter-1");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EmailTemplate_UpdateContent_bumps_UpdatedAtUtc()
    {
        var template = new EmailTemplate(
            tenantId: Guid.CreateVersion7(),
            slug: "interview-invite",
            name: "Interview Invite",
            subject: "Time to chat?",
            bodyMarkdown: "Body v1",
            createdByAuthSubject: "auth0|recruiter-1");

        var original = template.UpdatedAtUtc;
        // The clock has 100ns resolution but tests can run faster than that —
        // a tiny sleep guarantees the timestamps are observably different.
        Thread.Sleep(5);

        template.UpdateContent("Interview Invite v2", "New subject", "Body v2");

        template.Name.Should().Be("Interview Invite v2");
        template.Subject.Should().Be("New subject");
        template.BodyMarkdown.Should().Be("Body v2");
        template.UpdatedAtUtc.Should().BeAfter(original);
    }

    private sealed class StubCurrentTenant : ICurrentTenant
    {
        public Guid? TenantId => null;
    }
}
