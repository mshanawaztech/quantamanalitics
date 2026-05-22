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
public sealed class OnboardingSummaryEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public OnboardingSummaryEndpointTests(PostgresFixture postgres)
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
    public async Task Summary_aggregates_completion_per_consultant()
    {
        var tenantId = SeedOnboarding(_factory.Services);
        var client = _factory.WithAuthenticatedUser(
            tenantId,
            "auth0|recruiter-1",
            "recruiter@example.com",
            Roles.PlatformAdmin).CreateClient();

        var response = await client.GetAsync("/api/v1/onboarding/summary");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<OnboardingSummaryResponse>();
        payload.Should().NotBeNull();
        payload!.Consultants.Should().HaveCount(2);

        // Two consultants, one fully complete, one still in progress.
        payload.Totals.Consultants.Should().Be(2);
        payload.Totals.Complete.Should().Be(1);
        payload.Totals.InProgress.Should().Be(1);

        // Least-complete first.
        var first = payload.Consultants[0];
        first.Name.Should().Be("Bench Consultant");
        first.Total.Should().Be(3);
        first.Completed.Should().Be(1);
        first.InReview.Should().Be(1);
        first.Pending.Should().Be(1);
        first.CompletionPercent.Should().Be(33);
        first.Status.Should().Be("In review");

        var second = payload.Consultants[1];
        second.Name.Should().Be("Ready Consultant");
        second.CompletionPercent.Should().Be(100);
        second.Status.Should().Be("Complete");
    }

    private static Guid SeedOnboarding(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant("quantam", "Quantam Analytics");
        db.Tenants.Add(tenant);

        var ready = new CandidateProfile(tenant.Id, "guest|ready-1", "ready@example.com", "Ready Consultant");
        var bench = new CandidateProfile(tenant.Id, "guest|bench-1", "bench@example.com", "Bench Consultant");
        db.CandidateProfiles.AddRange(ready, bench);

        // Ready Consultant: 2 items, both approved → 100%.
        db.OnboardingChecklistItems.AddRange(
            Approved(tenant.Id, ready.Id, OnboardingItemType.W4, "W-4"),
            Approved(tenant.Id, ready.Id, OnboardingItemType.I9, "I-9"));

        // Bench Consultant: approved + submitted + pending → 33%, "In review".
        db.OnboardingChecklistItems.AddRange(
            Approved(tenant.Id, bench.Id, OnboardingItemType.W4, "W-4"),
            Submitted(tenant.Id, bench.Id, OnboardingItemType.I9, "I-9"),
            Pending(tenant.Id, bench.Id, OnboardingItemType.DirectDeposit, "Direct deposit"));

        db.SaveChanges();
        return tenant.Id;
    }

    private static OnboardingChecklistItem Pending(Guid tenantId, Guid profileId, OnboardingItemType type, string title)
        => new(tenantId, profileId, "auth0|recruiter-1", type, title, null);

    private static OnboardingChecklistItem Submitted(Guid tenantId, Guid profileId, OnboardingItemType type, string title)
    {
        var item = Pending(tenantId, profileId, type, title);
        item.Submit("uploaded");
        return item;
    }

    private static OnboardingChecklistItem Approved(Guid tenantId, Guid profileId, OnboardingItemType type, string title)
    {
        var item = Submitted(tenantId, profileId, type, title);
        item.Approve(null);
        return item;
    }
}
