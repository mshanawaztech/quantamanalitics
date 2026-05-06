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

public sealed class ClientApprovalEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ClientApprovalEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Timesheets_endpoint_returns_submitted_and_reviewed_items_but_not_drafts()
    {
        var seed = SeedTimesheets(_factory.Services);

        var client = _factory.WithAuthenticatedUser(
            seed.TenantId,
            "auth0|client-1",
            "client@example.com",
            "PlatformAdmin")
            .CreateClient();

        var response = await client.GetAsync("/api/v1/client/approvals/timesheets");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<ClientApprovalTimesheetsResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(3);
        payload.Items.Should().NotContain(x => x.Id == seed.DraftId);
        payload.Items.Should().Contain(x => x.Id == seed.SubmittedId && x.Status == "Submitted");
        payload.Items.Should().Contain(x => x.Id == seed.SecondSubmittedId && x.Status == "Submitted");
        payload.Items.Should().Contain(x => x.Id == seed.ApprovedId && x.Status == "Approved");
    }

    [Fact]
    public async Task Approve_and_reject_endpoints_transition_submitted_timesheets()
    {
        var seed = SeedTimesheets(_factory.Services);

        var client = _factory.WithAuthenticatedUser(
            seed.TenantId,
            "auth0|client-1",
            "client@example.com",
            "PlatformAdmin")
            .CreateClient();

        var approveResponse = await client.PostAsJsonAsync(
            $"/api/v1/client/approvals/timesheets/{seed.SubmittedId}/approve",
            new ReviewTimesheetRequest("Looks good"));

        var approveBody = await approveResponse.Content.ReadAsStringAsync();
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK, approveBody);

        var approved = await approveResponse.Content.ReadFromJsonAsync<ClientApprovalTimesheetResponse>();
        approved.Should().NotBeNull();
        approved!.Status.Should().Be("Approved");
        approved.ReviewNote.Should().Be("Looks good");

        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/v1/client/approvals/timesheets/{seed.SecondSubmittedId}/reject",
            new ReviewTimesheetRequest("Please correct PTO split"));

        var rejectBody = await rejectResponse.Content.ReadAsStringAsync();
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK, rejectBody);

        var rejected = await rejectResponse.Content.ReadFromJsonAsync<ClientApprovalTimesheetResponse>();
        rejected.Should().NotBeNull();
        rejected!.Status.Should().Be("Rejected");
        rejected.ReviewNote.Should().Be("Please correct PTO split");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = db.Timesheets
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == seed.TenantId)
            .ToArray();

        stored.Should().Contain(x => x.Id == seed.SubmittedId && x.Status == TimesheetStatus.Approved);
        stored.Should().Contain(x => x.Id == seed.SecondSubmittedId && x.Status == TimesheetStatus.Rejected);
    }

    private static SeededTimesheets SeedTimesheets(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Database.ExecuteSqlRaw("""
            drop table if exists time_entries;
            drop table if exists timesheets;

            create table if not exists timesheets (
              id uuid primary key,
              tenant_id uuid not null references tenants(id) on delete cascade,
              contractor_auth_subject character varying(200) not null,
              contractor_email character varying(320) not null,
              week_start_utc date not null,
              status character varying(32) not null,
              reviewed_by_auth_subject character varying(200),
              review_note character varying(1000),
              submitted_at_utc timestamp with time zone,
              reviewed_at_utc timestamp with time zone,
              created_at_utc timestamp with time zone not null,
              updated_at_utc timestamp with time zone not null
            );
            create unique index if not exists ix_timesheets_tenant_id_contractor_auth_subject_week_start_utc
              on timesheets (tenant_id, contractor_auth_subject, week_start_utc);

            create table if not exists time_entries (
              id uuid primary key,
              tenant_id uuid not null references tenants(id) on delete cascade,
              timesheet_id uuid not null references timesheets(id) on delete cascade,
              work_date date not null,
              hours numeric(5,2) not null,
              entry_type character varying(32) not null,
              notes character varying(500),
              created_at_utc timestamp with time zone not null,
              updated_at_utc timestamp with time zone not null
            );
            create unique index if not exists ix_time_entries_tenant_id_timesheet_id_work_date_entry_type
              on time_entries (tenant_id, timesheet_id, work_date, entry_type);

            delete from tenants;
            """);

        var tenant = new Tenant("quantam", "Quantam Analytics");
        db.Tenants.Add(tenant);

        var submitted = BuildTimesheet(tenant.Id, "auth0|contractor-1", "contractor1@example.com", new DateOnly(2026, 5, 4));
        submitted.Submit();

        var secondSubmitted = BuildTimesheet(tenant.Id, "auth0|contractor-2", "contractor2@example.com", new DateOnly(2026, 5, 11));
        secondSubmitted.Submit();

        var approved = BuildTimesheet(tenant.Id, "auth0|contractor-3", "contractor3@example.com", new DateOnly(2026, 5, 18));
        approved.Submit();
        approved.Approve("auth0|client-previous", "Already approved");

        var draft = BuildTimesheet(tenant.Id, "auth0|contractor-4", "contractor4@example.com", new DateOnly(2026, 5, 25));

        db.Timesheets.AddRange(submitted, secondSubmitted, approved, draft);
        db.SaveChanges();

        return new SeededTimesheets(tenant.Id, submitted.Id, secondSubmitted.Id, approved.Id, draft.Id);
    }

    private static Timesheet BuildTimesheet(Guid tenantId, string authSubject, string email, DateOnly weekStart)
    {
        var timesheet = new Timesheet(tenantId, authSubject, email, weekStart);
        timesheet.AddOrUpdateEntry(weekStart, 8, TimeEntryType.Work, null);
        timesheet.AddOrUpdateEntry(weekStart.AddDays(1), 8, TimeEntryType.Work, null);
        return timesheet;
    }

    private sealed record SeededTimesheets(
        Guid TenantId,
        Guid SubmittedId,
        Guid SecondSubmittedId,
        Guid ApprovedId,
        Guid DraftId);
}
