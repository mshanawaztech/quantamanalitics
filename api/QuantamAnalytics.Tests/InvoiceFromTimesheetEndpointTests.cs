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
public sealed class InvoiceFromTimesheetEndpointTests : IAsyncLifetime
{
    private const string ContractorSubject = "auth0|contractor-1";

    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public InvoiceFromTimesheetEndpointTests(PostgresFixture postgres)
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

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Approved_timesheet_becomes_draft_invoice_with_payable_hours()
    {
        var (tenantId, timesheetId) = SeedApprovedTimesheet(_factory.Services);
        var client = _factory.WithAuthenticatedUser(
            tenantId, ContractorSubject, "contractor@example.com", Roles.PlatformAdmin)
            .CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/contractor/invoices/from-timesheet",
            new CreateInvoiceFromTimesheetRequest(timesheetId, 100m, "Acme Client", null, null));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        invoice.Should().NotBeNull();
        invoice!.Status.Should().Be("Draft");
        invoice.ClientName.Should().Be("Acme Client");
        // 5 days × 8h = 40 payable hours × $100 = $4,000.
        invoice.Hours.Should().Be(40m);
        invoice.Amount.Should().Be(4000m);
        invoice.LineItems.Should().ContainSingle();
        invoice.LineItems[0].Hours.Should().Be(40m);
        invoice.LineItems[0].Rate.Should().Be(100m);
    }

    [Fact]
    public async Task Unapproved_timesheet_is_rejected()
    {
        var (tenantId, timesheetId) = SeedDraftTimesheet(_factory.Services);
        var client = _factory.WithAuthenticatedUser(
            tenantId, ContractorSubject, "contractor@example.com", Roles.PlatformAdmin)
            .CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/contractor/invoices/from-timesheet",
            new CreateInvoiceFromTimesheetRequest(timesheetId, 100m, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("approved");
    }

    private static (Guid TenantId, Guid TimesheetId) SeedApprovedTimesheet(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant("quantam", "Quantam Analytics");
        db.Tenants.Add(tenant);

        var timesheet = BuildFortyHourTimesheet(tenant.Id);
        timesheet.Submit();
        timesheet.Approve("auth0|client-1", null);

        db.Timesheets.Add(timesheet);
        db.SaveChanges();
        return (tenant.Id, timesheet.Id);
    }

    private static (Guid TenantId, Guid TimesheetId) SeedDraftTimesheet(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant("quantam", "Quantam Analytics");
        db.Tenants.Add(tenant);

        var timesheet = BuildFortyHourTimesheet(tenant.Id);
        db.Timesheets.Add(timesheet);
        db.SaveChanges();
        return (tenant.Id, timesheet.Id);
    }

    private static Timesheet BuildFortyHourTimesheet(Guid tenantId)
    {
        var timesheet = new Timesheet(
            tenantId, ContractorSubject, "contractor@example.com", new DateOnly(2026, 5, 4));

        var weekStart = timesheet.WeekStartUtc;
        for (var day = 0; day < 5; day++)
        {
            timesheet.AddOrUpdateEntry(weekStart.AddDays(day), 8m, TimeEntryType.Work, null);
        }

        return timesheet;
    }
}
