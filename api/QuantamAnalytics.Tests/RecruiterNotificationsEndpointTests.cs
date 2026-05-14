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
public sealed class RecruiterNotificationsEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public RecruiterNotificationsEndpointTests(PostgresFixture postgres)
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
    public async Task Notifications_returns_activity_stuck_pipeline_and_billing_alerts()
    {
        var tenantId = await SeedNotificationsAsync();
        var client = _factory
            .WithAuthenticatedUser(tenantId, "auth0|recruiter-1", "recruiter@example.com", Roles.PlatformAdmin)
            .CreateClient();

        var response = await client.GetAsync("/api/v1/recruiter/notifications");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<RecruiterNotificationsResponse>();
        payload.Should().NotBeNull();
        payload!.AttentionCount.Should().BeGreaterThan(0);
        payload.Items.Should().Contain(x => x.Category == "activity" && x.Title.Contains("Interview"));
        payload.Items.Should().Contain(x => x.Category == "pipeline" && x.Severity == "warning");
        payload.Items.Should().Contain(x => x.Category == "billing" && x.Severity == "success");
    }

    [Fact]
    public async Task Notifications_respects_tenant_isolation()
    {
        var (tenantAId, tenantBId) = await SeedTwoTenantsAsync();

        var client = _factory
            .WithAuthenticatedUser(tenantBId, "auth0|recruiter-b", "rb@example.com", Roles.Recruiter)
            .CreateClient();

        var response = await client.GetAsync("/api/v1/recruiter/notifications");
        var payload = await response.Content.ReadFromJsonAsync<RecruiterNotificationsResponse>();

        payload.Should().NotBeNull();
        payload!.Items.Should().OnlyContain(x => !x.Title.Contains("Casey Candidate"));
        payload.Items.Should().OnlyContain(x => !x.Detail.Contains("Senior Backend"));
        payload.Items.Should().ContainSingle(x => x.Category == "activity");
    }

    private async Task<Guid> SeedNotificationsAsync()
    {
        var (tenantAId, _) = await SeedTwoTenantsAsync();
        return tenantAId;
    }

    private async Task<(Guid TenantAId, Guid TenantBId)> SeedTwoTenantsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantA = new Tenant("acme", "Acme Staffing");
        var tenantB = new Tenant("globex", "Globex Talent");
        db.Tenants.AddRange(tenantA, tenantB);

        var jobA = new Job(tenantA.Id, "Senior Backend", "senior-backend", "Remote", "Backend role.", "Long description.", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)));
        var jobB = new Job(tenantB.Id, "Data Analyst", "data-analyst", "Austin", "Data role.", "Long description.", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)));
        db.Jobs.AddRange(jobA, jobB);

        var candidateA = new CandidateProfile(tenantA.Id, "auth0|candidate-a", "casey@example.com", "Casey Candidate");
        var candidateB = new CandidateProfile(tenantB.Id, "auth0|candidate-b", "rory@example.com", "Rory Candidate");
        db.CandidateProfiles.AddRange(candidateA, candidateB);

        var applicationA = new Application(tenantA.Id, jobA.Id, candidateA.Id, candidateA.Email, candidateA.FullName!, null);
        applicationA.TransitionToInterviewing();
        var applicationB = new Application(tenantB.Id, jobB.Id, candidateB.Id, candidateB.Email, candidateB.FullName!, null);
        db.Applications.AddRange(applicationA, applicationB);

        db.ApplicationTimelineEvents.Add(new ApplicationTimelineEvent(
            tenantA.Id,
            applicationA.Id,
            candidateA.Id,
            ApplicationTimelineEventType.InterviewScheduled,
            ApplicationTimelineAudience.CandidateAndRecruiter,
            "Interview confirmed",
            "Panel booked for Friday afternoon.",
            "Riley Recruiter",
            DateTimeOffset.UtcNow.AddHours(-4)));

        db.ApplicationTimelineEvents.Add(new ApplicationTimelineEvent(
            tenantB.Id,
            applicationB.Id,
            candidateB.Id,
            ApplicationTimelineEventType.RecruiterReviewed,
            ApplicationTimelineAudience.CandidateAndRecruiter,
            "Profile reviewed",
            "Looks strong for a first-round screen.",
            "Taylor Recruiter",
            DateTimeOffset.UtcNow.AddHours(-2)));

        var timesheet = new Timesheet(
            tenantA.Id,
            "auth0|contractor-a",
            "contractor@example.com",
            DateOnly.FromDateTime(DateTime.UtcNow.Date));
        timesheet.AddOrUpdateEntry(timesheet.WeekStartUtc, 8m, TimeEntryType.Work, null);
        timesheet.Submit();
        timesheet.Approve("auth0|recruiter-1", "Looks good.");
        db.Timesheets.Add(timesheet);

        await db.SaveChangesAsync();

        db.Database.ExecuteSqlInterpolated($"""
            update applications
            set updated_at_utc = {DateTimeOffset.UtcNow.AddDays(-9)}
            where id = {applicationA.Id};
            """);

        return (tenantA.Id, tenantB.Id);
    }
}
