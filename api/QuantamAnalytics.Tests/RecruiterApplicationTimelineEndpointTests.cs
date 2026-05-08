using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Tests.TestAuth;

namespace QuantamAnalytics.Tests;

public sealed class RecruiterApplicationTimelineEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RecruiterApplicationTimelineEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Recruiter_timeline_endpoint_returns_all_events_in_chronological_order()
    {
        var seed = SeedRecruiterTimeline(_factory.Services);

        var client = _factory.WithAuthenticatedUser(
            seed.TenantId,
            "auth0|recruiter-1",
            "recruiter@example.com",
            "PlatformAdmin")
            .CreateClient();

        var response = await client.GetAsync($"/api/v1/recruiter/applications/{seed.ApplicationId}/timeline");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<ApplicationTimelineResponse>();
        payload.Should().NotBeNull();
        payload!.Events.Should().HaveCount(3);
        payload.Events.Select(x => x.Title).Should().ContainInOrder(
            "Application received",
            "Internal recruiter note",
            "Moved to interviewing");
    }

    private static (Guid TenantId, Guid ApplicationId) SeedRecruiterTimeline(IServiceProvider services)
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

        var candidate = new CandidateProfile(
            tenant.Id,
            "guest|candidate-1",
            "jane@example.com",
            "Jane Candidate");

        var application = new Application(
            tenant.Id,
            job.Id,
            candidate.Id,
            candidate.Email,
            candidate.FullName ?? "Jane Candidate",
            "Internal note only.");
        application.TransitionToInterviewing();

        var appliedAtUtc = application.AppliedAtUtc;
        var recruiterOnlyAtUtc = appliedAtUtc.AddHours(4);
        var interviewingAtUtc = recruiterOnlyAtUtc.AddHours(20);

        db.Jobs.Add(job);
        db.CandidateProfiles.Add(candidate);
        db.Applications.Add(application);
        db.ApplicationTimelineEvents.AddRange(
            new ApplicationTimelineEvent(
                tenant.Id,
                application.Id,
                candidate.Id,
                ApplicationTimelineEventType.Applied,
                ApplicationTimelineAudience.CandidateAndRecruiter,
                "Application received",
                "The candidate completed the public application flow.",
                "Jane Candidate",
                appliedAtUtc),
            new ApplicationTimelineEvent(
                tenant.Id,
                application.Id,
                candidate.Id,
                ApplicationTimelineEventType.NoteAdded,
                ApplicationTimelineAudience.RecruiterOnly,
                "Internal recruiter note",
                "Internal note only.",
                "Recruiter Ops",
                recruiterOnlyAtUtc),
            new ApplicationTimelineEvent(
                tenant.Id,
                application.Id,
                candidate.Id,
                ApplicationTimelineEventType.StageChanged,
                ApplicationTimelineAudience.CandidateAndRecruiter,
                "Moved to interviewing",
                "The application progressed from screening into the interview stage.",
                "Recruiter Ops",
                interviewingAtUtc));

        db.SaveChanges();
        return (tenant.Id, application.Id);
    }
}
