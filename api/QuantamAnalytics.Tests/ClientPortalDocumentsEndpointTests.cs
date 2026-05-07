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
public sealed class ClientPortalDocumentsEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public ClientPortalDocumentsEndpointTests(PostgresFixture postgres)
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
    public async Task Listing_documents_returns_only_documents_at_the_clients_tenant()
    {
        var (tenantAId, tenantBId, _) = await SeedTwoTenantsWithDocumentForAAsync();

        var clientA = ClientForTenant(tenantAId, "auth0|client-a", "ca@a.example");
        var responseA = await clientA.GetAsync("/api/v1/client/documents");
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyA = await responseA.Content.ReadFromJsonAsync<ClientPortalDocumentsResponse>();
        bodyA!.Items.Should().ContainSingle();

        var clientB = ClientForTenant(tenantBId, "auth0|client-b", "cb@b.example");
        var responseB = await clientB.GetAsync("/api/v1/client/documents");
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyB = await responseB.Content.ReadFromJsonAsync<ClientPortalDocumentsResponse>();
        bodyB!.Items.Should().BeEmpty("tenant B has no documents and must not see tenant A's");
    }

    [Fact]
    public async Task Document_detail_for_other_tenants_id_returns_NotFound()
    {
        var (_, tenantBId, tenantADocId) = await SeedTwoTenantsWithDocumentForAAsync();

        var clientB = ClientForTenant(tenantBId, "auth0|client-b", "cb@b.example");
        var response = await clientB.GetAsync($"/api/v1/client/documents/{tenantADocId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Document_response_omits_internal_fields()
    {
        var (tenantAId, _, _) = await SeedTwoTenantsWithDocumentForAAsync();

        var clientA = ClientForTenant(tenantAId, "auth0|client-a", "ca@a.example");
        var response = await clientA.GetAsync("/api/v1/client/documents");
        var raw = await response.Content.ReadAsStringAsync();

        // Recruiter / provider-internal fields must not surface to clients.
        raw.Should().NotContain("\"sentByAuthSubject\"", "the recruiter who pushed the doc is internal to the staffing firm");
        raw.Should().NotContain("\"providerSubmissionId\"", "DocuSeal-internal id leaks provider coupling");
        raw.Should().NotContain("\"templateSlug\"", "template names are internal recruiter taxonomy");
    }

    private HttpClient ClientForTenant(Guid tenantId, string subject, string email) =>
        _factory
            .WithAuthenticatedUser(tenantId, subject, email, Roles.Client)
            .CreateClient();

    private async Task<(Guid TenantAId, Guid TenantBId, Guid TenantADocId)> SeedTwoTenantsWithDocumentForAAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantA = new Tenant("acme", "Acme Staffing");
        var tenantB = new Tenant("globex", "Globex Talent");
        db.Tenants.AddRange(tenantA, tenantB);

        var candidate = new CandidateProfile(
            tenantId: tenantA.Id,
            authSubject: "auth0|candidate-1",
            email: "candidate@example.com",
            fullName: "Casey Candidate");
        db.CandidateProfiles.Add(candidate);

        var doc = new EsignDocument(
            tenantId: tenantA.Id,
            candidateProfileId: candidate.Id,
            candidateEmail: "candidate@example.com",
            candidateName: "Casey Candidate",
            sentByAuthSubject: "auth0|recruiter-a",
            kind: EsignDocumentKind.OfferLetter,
            templateSlug: "offer_letter_v1",
            subject: "Your offer from Acme Staffing");
        db.EsignDocuments.Add(doc);

        await db.SaveChangesAsync();

        return (tenantA.Id, tenantB.Id, doc.Id);
    }
}
