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

public sealed class ContractorTimesheetEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ContractorTimesheetEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Current_endpoint_returns_empty_draft_when_week_has_no_timesheet()
    {
        var tenantId = SeedTenant(_factory.Services);

        var client = _factory.WithAuthenticatedUser(
            tenantId,
            "auth0|contractor-1",
            "contractor@example.com",
            "PlatformAdmin")
            .CreateClient();

        var response = await client.GetAsync("/api/v1/contractor/timesheets/current?weekStart=2026-05-04");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<ContractorTimesheetResponse>();
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("Draft");
        payload.WeekStartUtc.Should().Be(new DateOnly(2026, 5, 4));
        payload.Entries.Should().BeEmpty();
        payload.TotalHours.Should().Be(0);
        payload.Totals.PayableHours.Should().Be(0);
    }

    [Fact]
    public async Task Save_and_submit_endpoints_persist_current_week_entries()
    {
        var tenantId = SeedTenant(_factory.Services);

        var client = _factory.WithAuthenticatedUser(
            tenantId,
            "auth0|contractor-1",
            "contractor@example.com",
            "PlatformAdmin")
            .CreateClient();

        var request = new UpsertContractorTimesheetRequest(
            new DateOnly(2026, 5, 4),
            [
                new ContractorTimesheetEntryRequest(new DateOnly(2026, 5, 4), 8, "Work", null),
                new ContractorTimesheetEntryRequest(new DateOnly(2026, 5, 5), 4, "PaidTimeOff", null)
            ]);

        var saveResponse = await client.PutAsJsonAsync("/api/v1/contractor/timesheets/current", request);
        var saveBody = await saveResponse.Content.ReadAsStringAsync();

        saveResponse.StatusCode.Should().Be(HttpStatusCode.OK, saveBody);

        var saved = await saveResponse.Content.ReadFromJsonAsync<ContractorTimesheetResponse>();
        saved.Should().NotBeNull();
        saved!.Status.Should().Be("Draft");
        saved.TotalHours.Should().Be(12);
        saved.Totals.WorkHours.Should().Be(8);
        saved.Totals.PaidTimeOffHours.Should().Be(4);
        saved.Totals.RegularHours.Should().Be(8);
        saved.Totals.OvertimeHours.Should().Be(0);
        saved.Entries.Should().HaveCount(2);

        var submitResponse = await client.PostAsJsonAsync("/api/v1/contractor/timesheets/current/submit", request);
        var submitBody = await submitResponse.Content.ReadAsStringAsync();

        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK, submitBody);

        var submitted = await submitResponse.Content.ReadFromJsonAsync<ContractorTimesheetResponse>();
        submitted.Should().NotBeNull();
        submitted!.Status.Should().Be("Submitted");
        submitted.SubmittedAtUtc.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = db.Timesheets
            .IgnoreQueryFilters()
            .Include(x => x.Entries)
            .Single(x => x.TenantId == tenantId && x.ContractorAuthSubject == "auth0|contractor-1");

        persisted.Status.Should().Be(TimesheetStatus.Submitted);
        persisted.Entries.Should().HaveCount(2);
    }

    private static Guid SeedTenant(IServiceProvider services)
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
        db.SaveChanges();

        return tenant.Id;
    }
}
