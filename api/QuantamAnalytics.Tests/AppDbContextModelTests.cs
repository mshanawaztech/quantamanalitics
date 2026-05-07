using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Tests;

/// <summary>
/// Pure model-build tests — no real Postgres needed. Catches misconfigured
/// configurations (missing keys, wrong types, broken FKs) at unit-test speed.
/// Real DB-against-Testcontainers tests come later when there's enough behavior
/// to justify the spin-up cost.
/// </summary>
public sealed class AppDbContextModelTests
{
    private static AppDbContext NewContext()
    {
        // Connection string never opened — we only inspect the EF model.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=qa_dev_test;Username=u;Password=p")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new StubCurrentTenant());
    }

    [Fact]
    public void Model_includes_Tenant_entity()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(Tenant));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("tenants");
    }

    [Fact]
    public void Tenant_uses_snake_case_columns()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(Tenant))!;
        var columnNames = entity.GetProperties()
            .Select(p => p.GetColumnName())
            .ToArray();

        columnNames.Should().Contain(["id", "slug", "name", "created_at_utc", "is_active"]);
    }

    [Fact]
    public void Tenant_slug_has_unique_index()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(Tenant))!;
        var slugIndex = entity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Tenant.Slug)));

        slugIndex.Should().NotBeNull("slug is the natural tenant key — duplicates would break sign-in routing");
        slugIndex!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Tenant_id_is_not_db_generated_so_Guid_v7_is_preserved()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(Tenant))!;
        var idProperty = entity.FindProperty(nameof(Tenant.Id))!;

        idProperty.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never);
    }

    [Fact]
    public void Tenant_ctor_assigns_Guid_v7_id()
    {
        var t = new Tenant("acme", "Acme Staffing");

        t.Id.Should().NotBe(Guid.Empty);
        t.Id.Version.Should().Be(7);
        t.Slug.Should().Be("acme");
        t.Name.Should().Be("Acme Staffing");
        t.IsActive.Should().BeTrue();
        t.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Model_includes_CandidateProfile_entity()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(CandidateProfile));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("candidate_profiles");
    }

    [Fact]
    public void CandidateProfile_has_unique_tenant_subject_index()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(CandidateProfile))!;
        var subjectIndex = entity.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(CandidateProfile.TenantId), nameof(CandidateProfile.AuthSubject)]));

        subjectIndex.Should().NotBeNull();
        subjectIndex!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Model_includes_Application_entity()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(Application));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("applications");
    }

    [Fact]
    public void Model_includes_Submission_entity()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(Submission));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("submissions");
    }

    [Fact]
    public void Model_includes_Timesheet_entity()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(Timesheet));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("timesheets");
    }

    [Fact]
    public void Model_includes_InterviewEvent_entity()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(InterviewEvent));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("interview_events");
    }

    [Fact]
    public void Model_includes_TimeEntry_entity()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(TimeEntry));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("time_entries");
    }

    [Fact]
    public void Model_includes_BackgroundCheck_entity()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(BackgroundCheck));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("background_checks");
    }

    [Fact]
    public void BackgroundCheck_has_unique_provider_report_id_index()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(BackgroundCheck))!;
        var providerIndex = entity.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(BackgroundCheck.ProviderReportId)]));

        providerIndex.Should().NotBeNull();
        providerIndex!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Model_includes_EsignDocument_entity()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(EsignDocument));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("esign_documents");
    }

    [Fact]
    public void EsignDocument_has_unique_provider_submission_id_index()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(EsignDocument))!;
        var providerIndex = entity.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(EsignDocument.ProviderSubmissionId)]));

        providerIndex.Should().NotBeNull();
        providerIndex!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Timesheet_has_unique_tenant_contractor_week_index()
    {
        using var ctx = NewContext();

        var entity = ctx.Model.FindEntityType(typeof(Timesheet))!;
        var weekIndex = entity.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(Timesheet.TenantId), nameof(Timesheet.ContractorAuthSubject), nameof(Timesheet.WeekStartUtc)]));

        weekIndex.Should().NotBeNull();
        weekIndex!.IsUnique.Should().BeTrue();
    }

    private sealed class StubCurrentTenant : ICurrentTenant
    {
        public Guid? TenantId => null;
    }
}
