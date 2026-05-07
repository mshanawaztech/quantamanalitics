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
/// End-to-end proof that the EF Core global query filter on
/// <see cref="ITenantScoped"/> prevents one tenant from observing or
/// mutating another tenant's rows when going through the live API surface.
///
/// Setup is the canonical multi-tenant attack scenario:
/// - Two tenants exist (A and B) with their own recruiters
/// - Tenant A has rows in the database (jobs, candidate, application)
/// - Tenant B has no rows
/// - We assert that Tenant B's authenticated recruiter cannot see or modify
///   Tenant A's data through any tested endpoint, only their own
///
/// If the global query filter is ever disabled, weakened, or bypassed (e.g.
/// by a stray <c>IgnoreQueryFilters()</c> in production code), these tests
/// fail loudly. They are the safety net that lets us add new tenant-scoped
/// tables in Phase 4+ without re-auditing isolation by hand.
///
/// Coverage scope here is intentionally narrow — the recruiter Applications
/// endpoint, list and single-resource paths. The query-filter mechanism is
/// shared across every <see cref="ITenantScoped"/> entity, so two
/// representative cases is enough to prove the pattern. Add per-entity
/// coverage later if/when a real bug surfaces.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class TenantIsolationIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public TenantIsolationIntegrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = new IsolatedAppFactory(_postgres.ConnectionString);

        // Reset to a known schema for every test. EnsureDeleted + Migrate is
        // simple and bounded — schema is small, no test data lingers, and
        // each test seeds exactly what it needs.
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
    public async Task Recruiter_listing_applications_does_not_see_other_tenants_rows()
    {
        var (tenantAId, tenantBId, tenantAApplicationId) = await SeedTwoTenantsWithApplicationForAAsync();

        var responseA = await GetApplicationsAsync(tenantAId, "auth0|recruiter-a", "ra@a.example");
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyA = await responseA.Content.ReadFromJsonAsync<RecruiterApplicationsBoardResponse>();
        bodyA.Should().NotBeNull();
        bodyA!.Items.Should().HaveCount(1, "tenant A should see its own application");
        bodyA.Items[0].Id.Should().Be(tenantAApplicationId);

        var responseB = await GetApplicationsAsync(tenantBId, "auth0|recruiter-b", "rb@b.example");
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyB = await responseB.Content.ReadFromJsonAsync<RecruiterApplicationsBoardResponse>();
        bodyB.Should().NotBeNull();
        bodyB!.Items.Should().BeEmpty("tenant B has no applications and must not see tenant A's");
    }

    [Fact]
    public async Task Status_mutation_on_other_tenants_application_returns_NotFound()
    {
        var (_, tenantBId, tenantAApplicationId) = await SeedTwoTenantsWithApplicationForAAsync();

        // Tenant B's recruiter knows tenant A's application id (e.g. leaked
        // via logs or a guess). The query filter must hide the row, so the
        // single-row lookup returns null and the endpoint returns 404 —
        // never 200 and never accidentally mutating tenant A's row.
        var clientB = _factory
            .WithAuthenticatedUser(tenantBId, "auth0|recruiter-b", "rb@b.example", Roles.Recruiter)
            .CreateClient();

        var response = await clientB.PostAsJsonAsync(
            $"/api/v1/recruiter/applications/{tenantAApplicationId}/status",
            new UpdateApplicationStatusRequest("Interviewing"));

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "the tenant filter must hide tenant A's row from tenant B, so the lookup yields no row and the endpoint returns 404");
    }

    private async Task<HttpResponseMessage> GetApplicationsAsync(Guid tenantId, string subject, string email)
    {
        var client = _factory
            .WithAuthenticatedUser(tenantId, subject, email, Roles.Recruiter)
            .CreateClient();

        return await client.GetAsync("/api/v1/recruiter/applications");
    }

    /// <summary>
    /// Creates tenants A and B, gives tenant A a job and a candidate, and
    /// records one application from that candidate against tenant A's job.
    /// Returns the two tenant ids and the application id.
    /// </summary>
    private async Task<(Guid TenantAId, Guid TenantBId, Guid TenantAApplicationId)> SeedTwoTenantsWithApplicationForAAsync()
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

        return (tenantA.Id, tenantB.Id, application.Id);
    }
}
