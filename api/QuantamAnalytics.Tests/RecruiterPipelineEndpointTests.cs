using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Tests.Fixtures;
using QuantamAnalytics.Tests.TestAuth;

namespace QuantamAnalytics.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class RecruiterPipelineEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public RecruiterPipelineEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = new IsolatedAppFactory(_postgres.ConnectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Applications_board_returns_stage_age_and_stuck_flag()
    {
        var seed = SeedApplications(_factory.Services);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.ExecuteSqlInterpolated($"""
            update applications
            set updated_at_utc = {DateTimeOffset.UtcNow.AddDays(-9)}
            where id = {seed.AppliedId};
            """);

        var client = CreateRecruiterClient(seed.TenantId);

        var response = await client.GetAsync("/api/v1/recruiter/applications");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<RecruiterApplicationsBoardResponse>();
        payload.Should().NotBeNull();
        var applied = payload!.Items.Single(x => x.Id == seed.AppliedId);
        applied.IsStuck.Should().BeTrue();
        applied.DaysInStage.Should().BeGreaterThanOrEqualTo(9);
        payload.AvailableTags.Should().Contain("urgent");
        payload.AvailableLocations.Should().Contain("Dallas, TX · Hybrid");
        payload.SavedFilters.Should().ContainSingle(x => x.Name == "Urgent applied");
    }

    [Fact]
    public async Task Bulk_status_endpoint_updates_multiple_cards_and_records_timeline_events()
    {
        var seed = SeedApplications(_factory.Services);
        var client = CreateRecruiterClient(seed.TenantId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/recruiter/applications/bulk-status",
            new BulkUpdateApplicationStatusRequest(
                [seed.AppliedId, seed.SecondAppliedId],
                "Interviewing"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<RecruiterBulkStatusMoveResponse>();
        payload.Should().NotBeNull();
        payload!.RequestedCount.Should().Be(2);
        payload.UpdatedCount.Should().Be(2);
        payload.Items.Should().OnlyContain(x => x.Status == "Interviewing");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Applications.IgnoreQueryFilters()
            .Where(x => x.Id == seed.AppliedId || x.Id == seed.SecondAppliedId)
            .Should()
            .OnlyContain(x => x.Status == ApplicationStatus.Interviewing);

        db.ApplicationTimelineEvents.IgnoreQueryFilters()
            .Count(x => x.EventType == ApplicationTimelineEventType.StageChanged)
            .Should()
            .BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Applications_board_filters_by_search_tag_status_and_stuck()
    {
        var seed = SeedApplications(_factory.Services);
        var client = CreateRecruiterClient(seed.TenantId);

        var response = await client.GetAsync(
            "/api/v1/recruiter/applications?search=cloud&tag=urgent&status=Applied&stuckOnly=true");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<RecruiterApplicationsBoardResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items[0].Id.Should().Be(seed.AppliedId);
        payload.Items[0].Tags.Should().Contain("urgent");
    }

    [Fact]
    public async Task Bulk_tags_endpoint_adds_tags_to_multiple_cards()
    {
        var seed = SeedApplications(_factory.Services);
        var client = CreateRecruiterClient(seed.TenantId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/recruiter/applications/bulk-tags",
            new BulkUpdateApplicationTagsRequest(
                [seed.AppliedId, seed.SecondAppliedId],
                ["priority", "backend"],
                "Add"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<RecruiterBulkTagUpdateResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(2);
        payload.Items.Should().OnlyContain(x => x.Tags.Contains("priority"));
        payload.Items.Should().OnlyContain(x => x.Tags.Contains("backend"));
    }

    [Fact]
    public async Task Save_filter_endpoint_creates_and_lists_recruiter_preset()
    {
        var seed = SeedApplications(_factory.Services);
        var client = CreateRecruiterClient(seed.TenantId);

        var saveResponse = await client.PostAsJsonAsync(
            "/api/v1/recruiter/applications/filters",
            new SaveRecruiterApplicationFilterPresetRequest(
                null,
                "Offer follow-up",
                "jane",
                "Interviewing",
                "urgent",
                "Dallas",
                false));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();

        saveResponse.StatusCode.Should().Be(HttpStatusCode.OK, saveBody);

        var boardResponse = await client.GetAsync("/api/v1/recruiter/applications");
        var boardBody = await boardResponse.Content.ReadAsStringAsync();

        boardResponse.StatusCode.Should().Be(HttpStatusCode.OK, boardBody);

        var board = await boardResponse.Content.ReadFromJsonAsync<RecruiterApplicationsBoardResponse>();
        board.Should().NotBeNull();
        board!.SavedFilters.Should().Contain(x =>
            x.Name == "Offer follow-up" &&
            x.Tag == "urgent" &&
            x.Status == "Interviewing");
    }

    private HttpClient CreateRecruiterClient(Guid tenantId) =>
        _factory.WithAuthenticatedUser(
            tenantId,
            "auth0|recruiter-1",
            "recruiter@example.com",
            Roles.PlatformAdmin)
        .CreateClient();

    private static SeededApplications SeedApplications(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Database.ExecuteSqlRaw("""
            create table if not exists candidate_profiles (
              id uuid primary key,
              tenant_id uuid not null references tenants(id) on delete cascade,
              auth_subject character varying(200) not null,
              email character varying(320) not null,
              full_name character varying(160),
              phone_number character varying(40),
              headline character varying(160),
              summary character varying(4000),
              resume_object_key character varying(512),
              resume_file_name character varying(255),
              resume_uploaded_at_utc timestamp with time zone,
              created_at_utc timestamp with time zone not null,
              updated_at_utc timestamp with time zone not null
            );
            create unique index if not exists ix_candidate_profiles_tenant_id_auth_subject on candidate_profiles (tenant_id, auth_subject);
            create index if not exists ix_candidate_profiles_tenant_id_email on candidate_profiles (tenant_id, email);

            create table if not exists jobs (
              id uuid primary key,
              tenant_id uuid not null references tenants(id) on delete cascade,
              title character varying(160) not null,
              slug character varying(160) not null,
              location character varying(160) not null,
              summary character varying(320) not null,
              description character varying(4000) not null,
              posted_on_utc date not null,
              is_published boolean not null default true
            );
            create unique index if not exists ix_jobs_tenant_id_slug on jobs (tenant_id, slug);

            create table if not exists applications (
              id uuid primary key,
              tenant_id uuid not null references tenants(id) on delete cascade,
              job_id uuid not null references jobs(id) on delete cascade,
              candidate_profile_id uuid not null references candidate_profiles(id) on delete cascade,
              candidate_email character varying(320) not null,
              candidate_name character varying(160) not null,
              note character varying(4000),
              status character varying(32) not null,
              applied_at_utc timestamp with time zone not null,
              updated_at_utc timestamp with time zone not null
            );
            create unique index if not exists ix_applications_tenant_id_job_id_candidate_profile_id on applications (tenant_id, job_id, candidate_profile_id);
            create index if not exists ix_applications_tenant_id_status on applications (tenant_id, status);

            create table if not exists application_tags (
              id uuid primary key,
              tenant_id uuid not null references tenants(id) on delete cascade,
              application_id uuid not null references applications(id) on delete cascade,
              name character varying(64) not null,
              created_at_utc timestamp with time zone not null
            );
            create unique index if not exists ix_application_tags_tenant_id_application_id_name
              on application_tags (tenant_id, application_id, name);
            create index if not exists ix_application_tags_tenant_id_name
              on application_tags (tenant_id, name);

            create table if not exists recruiter_application_filter_presets (
              id uuid primary key,
              tenant_id uuid not null references tenants(id) on delete cascade,
              created_by_auth_subject character varying(200) not null,
              name character varying(120) not null,
              search character varying(160),
              status character varying(32),
              tag character varying(64),
              location character varying(160),
              stuck_only boolean not null,
              created_at_utc timestamp with time zone not null,
              updated_at_utc timestamp with time zone not null
            );
            create unique index if not exists ix_recruiter_application_filter_presets_tenant_id_created_by_auth_subject_name
              on recruiter_application_filter_presets (tenant_id, created_by_auth_subject, name);

            create table if not exists application_timeline_events (
              id uuid primary key,
              tenant_id uuid not null references tenants(id) on delete cascade,
              application_id uuid not null references applications(id) on delete cascade,
              candidate_profile_id uuid not null references candidate_profiles(id) on delete cascade,
              event_type character varying(48) not null,
              audience character varying(32) not null,
              title character varying(160) not null,
              description character varying(2000),
              actor_label character varying(160) not null,
              occurred_at_utc timestamp with time zone not null,
              created_at_utc timestamp with time zone not null
            );
            create index if not exists ix_application_timeline_events_tenant_id_application_id_occurred_at_utc
              on application_timeline_events (tenant_id, application_id, occurred_at_utc);
            create index if not exists ix_application_timeline_events_tenant_id_candidate_profile_id_occurred_at_utc
              on application_timeline_events (tenant_id, candidate_profile_id, occurred_at_utc);

            delete from application_timeline_events;
            delete from recruiter_application_filter_presets;
            delete from application_tags;
            delete from applications;
            delete from candidate_profiles;
            delete from jobs;
            delete from tenants;
            """);

        var tenant = new Tenant("quantam", "Quantam Analytics");
        db.Tenants.Add(tenant);

        var job = new Job(
            tenant.Id,
            "Technical Recruiter - Cloud & Data",
            "technical-recruiter-cloud-and-data",
            "Dallas, TX · Hybrid",
            "Drive sourcing and screening for cloud, data, and engineering roles.",
            "Join a staffing team focused on cloud and data talent.",
            DateOnly.FromDateTime(DateTime.UtcNow.Date));

        var firstCandidate = new CandidateProfile(
            tenant.Id,
            "guest|candidate-1",
            "jane@example.com",
            "Jane Candidate");

        var secondCandidate = new CandidateProfile(
            tenant.Id,
            "guest|candidate-2",
            "chris@example.com",
            "Chris Candidate");

        var applied = new Application(
            tenant.Id,
            job.Id,
            firstCandidate.Id,
            firstCandidate.Email,
            firstCandidate.FullName ?? "Jane Candidate",
            "Interested in cloud recruiting.");

        var secondApplied = new Application(
            tenant.Id,
            job.Id,
            secondCandidate.Id,
            secondCandidate.Email,
            secondCandidate.FullName ?? "Chris Candidate",
            "Strong sourcing background.");

        db.Jobs.Add(job);
        db.CandidateProfiles.AddRange(firstCandidate, secondCandidate);
        db.Applications.AddRange(applied, secondApplied);
        db.ApplicationTags.AddRange(
            new ApplicationTag(tenant.Id, applied.Id, "urgent"),
            new ApplicationTag(tenant.Id, applied.Id, "cloud"),
            new ApplicationTag(tenant.Id, secondApplied.Id, "backend"));
        db.RecruiterApplicationFilterPresets.Add(new RecruiterApplicationFilterPreset(
            tenant.Id,
            "auth0|recruiter-1",
            "Urgent applied",
            "jane",
            "Applied",
            "urgent",
            "Dallas",
            true));
        db.SaveChanges();

        db.Database.ExecuteSqlInterpolated($"""
            update applications
            set updated_at_utc = {DateTimeOffset.UtcNow.AddDays(-9)}
            where id = {applied.Id};
            """);

        return new SeededApplications(tenant.Id, applied.Id, secondApplied.Id);
    }

    private sealed record SeededApplications(
        Guid TenantId,
        Guid AppliedId,
        Guid SecondAppliedId);
}
