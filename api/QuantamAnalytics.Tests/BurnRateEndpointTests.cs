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
public sealed class BurnRateEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public BurnRateEndpointTests(PostgresFixture postgres)
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
    public async Task Burn_rate_aggregates_payable_hours_by_week_and_consultant()
    {
        var tenantId = SeedTimesheets(_factory.Services);
        var client = _factory.WithAuthenticatedUser(
            tenantId, "auth0|recruiter-1", "recruiter@example.com", Roles.PlatformAdmin)
            .CreateClient();

        var response = await client.GetAsync("/api/v1/recruiter/reports/burn-rate?weeks=12");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<BurnRateResponse>();
        payload.Should().NotBeNull();
        // 40h (consultant A) + 8h (consultant B) across two weeks.
        payload!.TotalPayableHours.Should().Be(48m);
        payload.Weeks.Should().HaveCount(2);
        payload.Consultants.Should().HaveCount(2);
        payload.Consultants[0].PayableHours.Should().Be(40m); // ordered desc
    }

    private static Guid SeedTimesheets(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant("quantam", "Quantam Analytics");
        db.Tenants.Add(tenant);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        // Consultant A: 40h two weeks ago, submitted.
        var a = new Timesheet(tenant.Id, "auth0|a", "a@example.com", today.AddDays(-14));
        var aStart = a.WeekStartUtc;
        for (var d = 0; d < 5; d++) a.AddOrUpdateEntry(aStart.AddDays(d), 8m, TimeEntryType.Work, null);
        a.Submit();

        // Consultant B: 8h last week, approved.
        var b = new Timesheet(tenant.Id, "auth0|b", "b@example.com", today.AddDays(-7));
        b.AddOrUpdateEntry(b.WeekStartUtc, 8m, TimeEntryType.Work, null);
        b.Submit();
        b.Approve("auth0|client-1", null);

        db.Timesheets.AddRange(a, b);
        db.SaveChanges();
        return tenant.Id;
    }
}
