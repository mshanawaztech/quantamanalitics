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
public sealed class ReportingEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public ReportingEndpointTests(PostgresFixture postgres)
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
    public async Task Summary_returns_funnel_counts_for_the_current_tenant()
    {
        var (tenantAId, _) = await SeedFunnelDataAsync();

        var client = ClientForTenant(tenantAId, "auth0|recruiter-a", "ra@a.example");
        var response = await client.GetAsync("/api/v1/recruiter/reports/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<ReportingSummaryResponse>();
        summary.Should().NotBeNull();
        summary!.Funnel.Applied.Should().Be(2);
        summary.Funnel.Interviewing.Should().Be(1);
        summary.Funnel.Hired.Should().Be(1);
        summary.Funnel.Rejected.Should().Be(0, "missing statuses must render as zero");
    }

    [Fact]
    public async Task Summary_funnel_excludes_other_tenants_applications()
    {
        var (_, tenantBId) = await SeedFunnelDataAsync();

        var client = ClientForTenant(tenantBId, "auth0|recruiter-b", "rb@b.example");
        var response = await client.GetAsync("/api/v1/recruiter/reports/summary");

        var summary = await response.Content.ReadFromJsonAsync<ReportingSummaryResponse>();
        summary!.Funnel.Applied.Should().Be(0);
        summary.Funnel.Interviewing.Should().Be(0);
        summary.Funnel.Hired.Should().Be(0, "tenant B has no applications and must not see tenant A's funnel");
        summary.RecruiterActivity.Should().BeEmpty();
        summary.TimeToFill.TotalJobsHired.Should().Be(0);
    }

    [Fact]
    public async Task Time_to_fill_returns_null_average_when_no_jobs_have_been_hired()
    {
        // Seed a tenant with applications that never reach Hired.
        Guid tenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenant = new Tenant("acme", "Acme Staffing");
            db.Tenants.Add(tenant);

            var job = new Job(tenant.Id, "QA", "qa", "Remote", "QA role.", "Description.", DateOnly.FromDateTime(DateTime.UtcNow));
            db.Jobs.Add(job);

            var candidate = new CandidateProfile(tenant.Id, "auth0|c", "c@example.com", "Casey Candidate");
            db.CandidateProfiles.Add(candidate);

            db.Applications.Add(new Application(tenant.Id, job.Id, candidate.Id, "c@example.com", "Casey", null));
            await db.SaveChangesAsync();

            tenantId = tenant.Id;
        }

        var client = ClientForTenant(tenantId, "auth0|recruiter", "r@example.com");
        var response = await client.GetAsync("/api/v1/recruiter/reports/summary");

        var summary = await response.Content.ReadFromJsonAsync<ReportingSummaryResponse>();
        summary!.TimeToFill.TotalJobsHired.Should().Be(0);
        summary.TimeToFill.AverageDays.Should().BeNull();
    }

    private HttpClient ClientForTenant(Guid tenantId, string subject, string email) =>
        _factory
            .WithAuthenticatedUser(tenantId, subject, email, Roles.Recruiter)
            .CreateClient();

    /// <summary>
    /// Seeds two tenants. Tenant A has 4 applications across statuses
    /// (Applied×2, Interviewing×1, Hired×1) plus one Submission credited
    /// to a recruiter — enough to exercise every section of the summary.
    /// Tenant B is empty.
    /// </summary>
    private async Task<(Guid TenantAId, Guid TenantBId)> SeedFunnelDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantA = new Tenant("acme", "Acme Staffing");
        var tenantB = new Tenant("globex", "Globex Talent");
        db.Tenants.AddRange(tenantA, tenantB);

        var job = new Job(tenantA.Id, "Senior Backend", "senior-backend", "Remote", "Backend role.", "Long description.", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
        db.Jobs.Add(job);

        var candidate = new CandidateProfile(tenantA.Id, "auth0|c", "c@example.com", "Casey Candidate");
        db.CandidateProfiles.Add(candidate);

        var applied1 = new Application(tenantA.Id, job.Id, candidate.Id, "c@example.com", "Casey Candidate", null);
        var applied2 = new Application(tenantA.Id, job.Id, candidate.Id, "c2@example.com", "Casey Two", null);
        var interviewing = new Application(tenantA.Id, job.Id, candidate.Id, "c3@example.com", "Casey Three", null);
        interviewing.TransitionToInterviewing();
        var hired = new Application(tenantA.Id, job.Id, candidate.Id, "c4@example.com", "Casey Four", null);
        hired.TransitionToInterviewing();
        hired.TransitionToOfferSent();
        hired.TransitionToHired();
        db.Applications.AddRange(applied1, applied2, interviewing, hired);

        var submission = new Submission(
            tenantId: tenantA.Id,
            applicationId: hired.Id,
            jobId: job.Id,
            candidateProfileId: candidate.Id,
            candidateEmail: "c4@example.com",
            candidateName: "Casey Four");
        submission.SubmitToClient("auth0|recruiter-a", "Big Bank Co", "Strong systems background.");
        db.Submissions.Add(submission);

        await db.SaveChangesAsync();

        return (tenantA.Id, tenantB.Id);
    }
}
