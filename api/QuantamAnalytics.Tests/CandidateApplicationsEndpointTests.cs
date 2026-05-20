using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Tests.Fixtures;
using QuantamAnalytics.Tests.TestAuth;

namespace QuantamAnalytics.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class CandidateApplicationsEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public CandidateApplicationsEndpointTests(PostgresFixture postgres)
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
    public async Task Applications_endpoint_returns_guest_application_after_candidate_signs_in()
    {
        var tenantId = SeedCandidateApplication(_factory.Services);

        var client = _factory.WithAuthenticatedUser(
            tenantId,
            "auth0|candidate-1",
            "jane@example.com",
            "Candidate")
            .CreateClient();

        var response = await client.GetAsync("/api/v1/candidate/profile/applications");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<CandidateApplicationsResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items[0].JobSlug.Should().Be("technical-recruiter-cloud-and-data");
        payload.Items[0].Status.Should().Be("Applied");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CandidateProfiles.IgnoreQueryFilters()
            .Should()
            .ContainSingle(x => x.Email == "jane@example.com" && x.AuthSubject == "auth0|candidate-1");
    }

    private static Guid SeedCandidateApplication(IServiceProvider services)
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
            "Drive sourcing and screening for cloud, data, and engineering roles while improving reusable search playbooks.",
            "Join a staffing team focused on cloud and data talent.",
            DateOnly.FromDateTime(DateTime.UtcNow.Date));

        var guestProfile = new CandidateProfile(
            tenant.Id,
            "guest|candidate-1",
            "jane@example.com",
            "Jane Candidate");

        var application = new Application(
            tenant.Id,
            job.Id,
            guestProfile.Id,
            guestProfile.Email,
            guestProfile.FullName ?? "Jane Candidate",
            "Already applied as a guest.");

        db.Jobs.Add(job);
        db.CandidateProfiles.Add(guestProfile);
        db.Applications.Add(application);
        db.SaveChanges();

        return tenant.Id;
    }
}
