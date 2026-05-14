using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Tests.TestAuth;

namespace QuantamAnalytics.Tests;

public sealed class InterviewSchedulingEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InterviewSchedulingEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Overview_returns_provider_baseline_and_tenant_events()
    {
        var tenantId = SeedInterviewData(_factory.Services);
        using var client = _factory
            .WithAuthenticatedUser(tenantId, "auth0|interviewer-1", "interviewer@example.com", Roles.Interviewer)
            .CreateClient();

        var response = await client.GetAsync("/api/v1/interviews/overview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<InterviewOverviewResponse>();
        payload.Should().NotBeNull();
        payload!.Providers.Should().Contain(x => x.Name == "Google Calendar baseline");
        payload.Events.Should().ContainSingle();
        payload.Events[0].CandidateEmail.Should().Be("jane@example.com");
    }

    [Fact]
    public async Task Overview_returns_403_for_payroll_only_session()
    {
        var tenantId = Guid.NewGuid();
        using var client = _factory
            .WithAuthenticatedUser(tenantId, "auth0|payroll-1", "payroll@example.com", Roles.PayrollAdmin)
            .CreateClient();

        var response = await client.GetAsync("/api/v1/interviews/overview");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Guid SeedInterviewData(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.InterviewEvents.IgnoreQueryFilters().ExecuteDelete();
        db.Submissions.IgnoreQueryFilters().ExecuteDelete();
        db.Applications.IgnoreQueryFilters().ExecuteDelete();
        db.CandidateProfiles.IgnoreQueryFilters().ExecuteDelete();
        db.Jobs.IgnoreQueryFilters().ExecuteDelete();
        db.Tenants.IgnoreQueryFilters().ExecuteDelete();

        var tenant = new Tenant("quantam-tests", "Quantam Tests");
        var job = new Job(
            tenant.Id,
            "Technical Recruiter",
            "technical-recruiter",
            "Remote",
            "Summary",
            "Description",
            new DateOnly(2026, 5, 1));
        var candidate = new CandidateProfile(tenant.Id, "auth0|candidate-1", "jane@example.com", "Jane Candidate");
        var application = new Application(
            tenant.Id,
            job.Id,
            candidate.Id,
            candidate.Email,
            candidate.FullName ?? "Jane Candidate",
            "Ready for client screen");
        application.TransitionToInterviewing();

        var submission = new Submission(
            tenant.Id,
            application.Id,
            job.Id,
            candidate.Id,
            candidate.Email,
            candidate.FullName ?? "Jane Candidate");
        submission.SubmitToClient("auth0|recruiter-1", "Acme Client", "Strong fit");
        submission.MarkClientReviewing();

        var start = new DateTimeOffset(2026, 5, 20, 15, 0, 0, TimeSpan.Zero);
        var interview = new InterviewEvent(
            tenant.Id,
            submission.Id,
            submission.CandidateName,
            submission.CandidateEmail,
            "Client screening panel",
            "Hiring manager",
            InterviewCalendarProvider.GoogleCalendar,
            start,
            start.AddHours(1));

        db.Tenants.Add(tenant);
        db.Jobs.Add(job);
        db.CandidateProfiles.Add(candidate);
        db.Applications.Add(application);
        db.Submissions.Add(submission);
        db.InterviewEvents.Add(interview);
        db.SaveChanges();

        return tenant.Id;
    }
}
