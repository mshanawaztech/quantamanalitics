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
public sealed class OnboardingTemplateEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public OnboardingTemplateEndpointTests(PostgresFixture postgres)
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
    public async Task Apply_template_creates_phased_checklist_and_is_idempotent()
    {
        var (tenantId, candidateId) = SeedCandidate(_factory.Services);
        var client = _factory.WithAuthenticatedUser(
            tenantId, "auth0|recruiter-1", "recruiter@example.com", Roles.PlatformAdmin)
            .CreateClient();

        var first = await client.PostAsJsonAsync(
            $"/api/v1/onboarding/candidates/{candidateId}/apply-template",
            new ApplyOnboardingTemplateBody(ConsultantType.Contractor));
        var body = await first.Content.ReadAsStringAsync();
        first.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payload = await first.Content.ReadFromJsonAsync<OnboardingChecklistResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(13);
        // Phased: every item carries a phase, and the multiple phases are present.
        payload.Items.Should().OnlyContain(x => !string.IsNullOrEmpty(x.Phase));
        payload.Items.Select(x => x.Phase).Distinct().Should().HaveCountGreaterThanOrEqualTo(4);
        // Ordered by phase/sort order.
        payload.Items.Should().BeInAscendingOrder(x => x.SortOrder);

        // Re-applying the same template adds nothing (idempotent).
        var second = await client.PostAsJsonAsync(
            $"/api/v1/onboarding/candidates/{candidateId}/apply-template",
            new ApplyOnboardingTemplateBody(ConsultantType.Contractor));
        var secondPayload = await second.Content.ReadFromJsonAsync<OnboardingChecklistResponse>();
        secondPayload!.Items.Should().HaveCount(13);
    }

    private static (Guid TenantId, Guid CandidateId) SeedCandidate(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant("quantam", "Quantam Analytics");
        db.Tenants.Add(tenant);
        var candidate = new CandidateProfile(tenant.Id, "guest|c1", "c1@example.com", "Casey Consultant");
        db.CandidateProfiles.Add(candidate);
        db.SaveChanges();
        return (tenant.Id, candidate.Id);
    }
}
