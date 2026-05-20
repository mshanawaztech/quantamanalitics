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
public sealed class RecruiterInvoiceReadyEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public RecruiterInvoiceReadyEndpointTests(PostgresFixture postgres)
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
    public async Task Invoice_ready_endpoint_returns_only_approved_timesheets_with_totals()
    {
        var tenantId = SeedApprovedTimesheets(_factory.Services);

        var client = _factory.WithAuthenticatedUser(
            tenantId,
            "auth0|recruiter-1",
            "recruiter@example.com",
            "PlatformAdmin")
            .CreateClient();

        var response = await client.GetAsync("/api/v1/recruiter/invoice-ready");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<RecruiterInvoiceReadyResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items[0].RegularHours.Should().Be(40);
        payload.Items[0].OvertimeHours.Should().Be(6);
        payload.Items[0].PaidTimeOffHours.Should().Be(4);
        payload.Items[0].PayableHours.Should().Be(50);
    }

    [Fact]
    public async Task Invoice_handoff_endpoint_returns_stripe_fallback_batch()
    {
        var tenantId = SeedApprovedTimesheets(_factory.Services);

        var client = _factory.WithAuthenticatedUser(
            tenantId,
            "auth0|recruiter-1",
            "recruiter@example.com",
            "PlatformAdmin")
            .CreateClient();

        var response = await client.GetAsync("/api/v1/recruiter/invoice-handoff");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<RecruiterInvoiceHandoffResponse>();
        payload.Should().NotBeNull();
        payload!.ApprovedTimesheetCount.Should().Be(1);
        payload.TotalPayableHours.Should().Be(50);
        payload.StripeFallback.Items.Should().ContainSingle();
        payload.StripeFallback.Items[0].CollectionMethod.Should().Be("send_invoice");
        payload.StripeFallback.Items[0].Quantity.Should().Be(50);
    }

    [Fact]
    public async Task Quickbooks_csv_export_returns_only_approved_timesheets()
    {
        var tenantId = SeedApprovedTimesheets(_factory.Services);

        var client = _factory.WithAuthenticatedUser(
            tenantId,
            "auth0|recruiter-1",
            "recruiter@example.com",
            "PlatformAdmin")
            .CreateClient();

        var response = await client.GetAsync("/api/v1/recruiter/invoice-handoff/quickbooks.csv");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        body.Should().Contain("contractor@example.com");
        body.Should().NotContain("contractor2@example.com");
        body.Should().Contain("payable_hours");
    }

    private static Guid SeedApprovedTimesheets(IServiceProvider services)
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

        var approved = new Timesheet(tenant.Id, "auth0|contractor-1", "contractor@example.com", new DateOnly(2026, 5, 4));
        approved.AddOrUpdateEntry(new DateOnly(2026, 5, 4), 10, TimeEntryType.Work, null);
        approved.AddOrUpdateEntry(new DateOnly(2026, 5, 5), 10, TimeEntryType.Work, null);
        approved.AddOrUpdateEntry(new DateOnly(2026, 5, 6), 10, TimeEntryType.Work, null);
        approved.AddOrUpdateEntry(new DateOnly(2026, 5, 7), 10, TimeEntryType.Work, null);
        approved.AddOrUpdateEntry(new DateOnly(2026, 5, 8), 6, TimeEntryType.Work, null);
        approved.AddOrUpdateEntry(new DateOnly(2026, 5, 9), 4, TimeEntryType.PaidTimeOff, null);
        approved.Submit();
        approved.Approve("auth0|client-1", "Approved");

        var submitted = new Timesheet(tenant.Id, "auth0|contractor-2", "contractor2@example.com", new DateOnly(2026, 5, 11));
        submitted.AddOrUpdateEntry(new DateOnly(2026, 5, 11), 8, TimeEntryType.Work, null);
        submitted.Submit();

        db.Timesheets.AddRange(approved, submitted);
        db.SaveChanges();

        return tenant.Id;
    }
}
