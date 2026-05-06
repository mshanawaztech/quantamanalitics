using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;

namespace QuantamAnalytics.Tests;

public sealed class PublicJobsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PublicJobsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Jobs_list_returns_seeded_public_jobs()
    {
        var client = CreateClientWithInitializedSchema();

        var response = await client.GetAsync("/api/v1/jobs");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var jobs = await response.Content.ReadFromJsonAsync<PublicJobListItemResponse[]>();
        jobs.Should().NotBeNull();
        jobs!.Should().NotBeEmpty();
        jobs.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Slug));
    }

    [Fact]
    public async Task Job_detail_returns_single_job_by_slug()
    {
        var client = CreateClientWithInitializedSchema();
        var jobs = await client.GetFromJsonAsync<PublicJobListItemResponse[]>("/api/v1/jobs");

        var response = await client.GetAsync($"/api/v1/jobs/{jobs![0].Slug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var job = await response.Content.ReadFromJsonAsync<PublicJobDetailResponse>();
        job.Should().NotBeNull();
        job!.Slug.Should().Be(jobs[0].Slug);
    }

    [Fact]
    public async Task Job_detail_returns_404_for_unknown_slug()
    {
        var client = CreateClientWithInitializedSchema();

        var response = await client.GetAsync("/api/v1/jobs/does-not-exist");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, body);
    }

    [Fact]
    public async Task Apply_creates_candidate_profile_and_application()
    {
        var client = CreateClientWithInitializedSchema();
        var jobs = await client.GetFromJsonAsync<PublicJobListItemResponse[]>("/api/v1/jobs");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/jobs/{jobs![0].Slug}/apply",
            new PublicJobApplicationRequest(
                "Jane Candidate",
                "jane@example.com",
                "Strong fit for this opening."));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CandidateProfiles.IgnoreQueryFilters().Should().ContainSingle(x => x.Email == "jane@example.com");
        db.Applications.IgnoreQueryFilters().Should().ContainSingle(x => x.CandidateEmail == "jane@example.com");
    }

    private HttpClient CreateClientWithInitializedSchema()
    {
        InitializeJobsSchema(_factory.Services);
        SeedDemoData(_factory.Services);
        return _factory.CreateClient();
    }

    private static void InitializeJobsSchema(IServiceProvider services)
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
            """);
    }

    private static void SeedDemoData(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!db.Tenants.Any())
        {
            db.Tenants.Add(new Tenant("quantam", "Quantam Analytics"));
            db.SaveChanges();
        }

        var tenant = db.Tenants.Single(x => x.Slug == "quantam");

        if (!db.Jobs.IgnoreQueryFilters().Any())
        {
            db.Jobs.AddRange(
                new Job(
                    tenant.Id,
                    "Senior .NET Staffing Solutions Lead",
                    "senior-dotnet-staffing-solutions-lead",
                    "Remote · United States",
                    "Own recruiter collaboration, client intake, and candidate workflow design for a growing staffing operation.",
                    "Lead the shaping of recruiter-facing workflow inside a modern staffing platform. You will partner with delivery leadership, turn recruiting process pain into software requirements, and help operationalize better candidate submission velocity.",
                    DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-4))),
                new Job(
                    tenant.Id,
                    "Technical Recruiter - Cloud & Data",
                    "technical-recruiter-cloud-and-data",
                    "Dallas, TX · Hybrid",
                    "Drive sourcing and screening for cloud, data, and engineering roles while improving reusable search playbooks.",
                    "Join a staffing team focused on cloud and data talent. This role blends hands-on sourcing, hiring-manager calibration, candidate storytelling, and lightweight process improvement across the submission funnel.",
                    DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1))));
            db.SaveChanges();
        }
    }
}
