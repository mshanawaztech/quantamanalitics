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

/// <summary>
/// Integration coverage for the read-only client portal. The same isolation
/// shape PR-30 enforces on the recruiter Applications surface — applied to
/// every client-side route. Tenant A's client must not see tenant B's
/// jobs, applications, or submissions through any of the new endpoints.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ClientPortalJobsEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public ClientPortalJobsEndpointTests(PostgresFixture postgres)
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
    public async Task Listing_jobs_returns_only_jobs_at_the_clients_tenant()
    {
        var (tenantAId, tenantBId, _, _) = await SeedTwoTenantsWithJobAndApplicationAsync();

        var clientA = ClientForTenant(tenantAId, "auth0|client-a", "ca@a.example");
        var responseA = await clientA.GetAsync("/api/v1/client/jobs");
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyA = await responseA.Content.ReadFromJsonAsync<ClientPortalJobsResponse>();
        bodyA!.Items.Should().ContainSingle();
        bodyA.Items[0].Title.Should().Be("Senior Backend Engineer");

        var clientB = ClientForTenant(tenantBId, "auth0|client-b", "cb@b.example");
        var responseB = await clientB.GetAsync("/api/v1/client/jobs");
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyB = await responseB.Content.ReadFromJsonAsync<ClientPortalJobsResponse>();
        bodyB!.Items.Should().BeEmpty("tenant B has no jobs of its own and must not see tenant A's");
    }

    [Fact]
    public async Task Job_detail_for_other_tenants_job_returns_NotFound()
    {
        var (_, tenantBId, tenantAJobId, _) = await SeedTwoTenantsWithJobAndApplicationAsync();

        var clientB = ClientForTenant(tenantBId, "auth0|client-b", "cb@b.example");
        var response = await clientB.GetAsync($"/api/v1/client/jobs/{tenantAJobId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Applications_for_other_tenants_job_returns_NotFound()
    {
        var (_, tenantBId, tenantAJobId, _) = await SeedTwoTenantsWithJobAndApplicationAsync();

        var clientB = ClientForTenant(tenantBId, "auth0|client-b", "cb@b.example");
        var response = await clientB.GetAsync($"/api/v1/client/jobs/{tenantAJobId}/applications");

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "the job is invisible to tenant B's filter, so the existence check returns false and the endpoint 404s — never an empty list (which would leak that the id is taken)");
    }

    [Fact]
    public async Task Applications_endpoint_returns_pipeline_for_owned_job()
    {
        var (tenantAId, _, tenantAJobId, _) = await SeedTwoTenantsWithJobAndApplicationAsync();

        var clientA = ClientForTenant(tenantAId, "auth0|client-a", "ca@a.example");
        var response = await clientA.GetAsync($"/api/v1/client/jobs/{tenantAJobId}/applications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ClientPortalApplicationsResponse>();
        body!.Items.Should().ContainSingle();
        body.Items[0].CandidateName.Should().Be("Casey Candidate");
        body.Items[0].CandidateEmail.Should().Be("candidate@example.com");
    }

    private HttpClient ClientForTenant(Guid tenantId, string subject, string email) =>
        _factory
            .WithAuthenticatedUser(tenantId, subject, email, Roles.Client)
            .CreateClient();

    private async Task<(Guid TenantAId, Guid TenantBId, Guid TenantAJobId, Guid TenantAApplicationId)> SeedTwoTenantsWithJobAndApplicationAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantA = new Tenant("acme", "Acme Staffing");
        var tenantB = new Tenant("globex", "Globex Talent");
        db.Tenants.AddRange(tenantA, tenantB);

        var job = new Job(
            tenantId: tenantA.Id,
            title: "Senior Backend Engineer",
            slug: "senior-backend-engineer",
            location: "Remote",
            summary: "Backend role.",
            description: "Long description.",
            postedOnUtc: DateOnly.FromDateTime(DateTime.UtcNow));
        db.Jobs.Add(job);

        var candidate = new CandidateProfile(
            tenantId: tenantA.Id,
            authSubject: "auth0|candidate-1",
            email: "candidate@example.com",
            fullName: "Casey Candidate");
        db.CandidateProfiles.Add(candidate);

        var application = new Application(
            tenantId: tenantA.Id,
            jobId: job.Id,
            candidateProfileId: candidate.Id,
            candidateEmail: "candidate@example.com",
            candidateName: "Casey Candidate",
            note: null);
        db.Applications.Add(application);

        await db.SaveChangesAsync();

        return (tenantA.Id, tenantB.Id, job.Id, application.Id);
    }
}
