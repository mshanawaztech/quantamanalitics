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

    private HttpClient CreateClientWithInitializedSchema()
    {
        InitializeJobsSchema(_factory.Services);
        SeedTenant(_factory.Services);
        return _factory.CreateClient();
    }

    private static void InitializeJobsSchema(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Database.ExecuteSqlRaw("""
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
            delete from jobs;
            """);
    }

    private static void SeedTenant(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.Any())
        {
            return;
        }

        db.Tenants.Add(new Tenant("quantam", "Quantam Analytics"));
        db.SaveChanges();
    }
}
