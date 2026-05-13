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

/// <summary>
/// End-to-end coverage for the recruiter email-template CRUD surface.
/// Same fixture pattern as the Phase-4 client-portal tests — Postgres
/// container + isolated WebApplicationFactory + the test auth handler so
/// every assertion exercises the real auth + tenant-filter wiring.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class EmailTemplateEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public EmailTemplateEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = new IsolatedAppFactory(_postgres.ConnectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Recruiter_creates_a_template_and_GETs_it_back_in_the_list()
    {
        var tenantId = await SeedTenantAsync("acme", "Acme Staffing");

        var recruiter = RecruiterFor(tenantId, "auth0|recruiter-1", "r@a.example");

        var createResponse = await recruiter.PostAsJsonAsync("/api/v1/recruiter/email-templates", new
        {
            slug = "interview-invite",
            name = "Interview Invite",
            subject = "Time to chat?",
            bodyMarkdown = "Hi {{candidate}}, are you free for a 30-min screen?",
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, await createResponse.Content.ReadAsStringAsync());
        createResponse.Headers.Location.Should().NotBeNull();

        var listResponse = await recruiter.GetAsync("/api/v1/recruiter/email-templates");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.Content.ReadFromJsonAsync<EmailTemplateListResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle();
        body.Items[0].Slug.Should().Be("interview-invite");
        body.Items[0].Name.Should().Be("Interview Invite");
        body.Items[0].CreatedByAuthSubject.Should().Be("auth0|recruiter-1");
    }

    [Fact]
    public async Task Catalog_returns_starter_templates_and_supported_merge_fields()
    {
        var tenantId = await SeedTenantAsync("acme", "Acme Staffing");
        var recruiter = RecruiterFor(tenantId, "auth0|recruiter-1", "r@a.example");

        var response = await recruiter.GetAsync("/api/v1/recruiter/email-templates/catalog");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EmailTemplateCatalogResponse>();
        body.Should().NotBeNull();
        body!.Presets.Should().Contain(x => x.Slug == "interview-invite");
        body.Presets.Should().Contain(x => x.Slug == "rejection-note");
        body.SupportedMergeFields.Should().Contain("candidate_name");
        body.SupportedMergeFields.Should().Contain("job_title");
    }

    [Fact]
    public async Task Preview_renders_merge_fields_without_persisting_anything()
    {
        var tenantId = await SeedTenantAsync("acme", "Acme Staffing");
        var recruiter = RecruiterFor(tenantId, "auth0|recruiter-1", "r@a.example");

        var response = await recruiter.PostAsJsonAsync("/api/v1/recruiter/email-templates/preview", new
        {
            subject = "Interview invite for {{job_title}}",
            bodyMarkdown = "Hi {{candidate_name}}, meet {{recruiter_name}} at {{company}}.",
            mergeFields = new Dictionary<string, string?>
            {
                ["candidate_name"] = "Jane Candidate",
                ["recruiter_name"] = "Riley Recruiter",
                ["company"] = "Acme Staffing",
                ["job_title"] = "Cloud Recruiter"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EmailTemplatePreviewResponse>();
        body.Should().NotBeNull();
        body!.Subject.Should().Be("Interview invite for Cloud Recruiter");
        body.BodyMarkdown.Should().Contain("Hi Jane Candidate");
        body.BodyMarkdown.Should().Contain("Riley Recruiter");
        body.BodyMarkdown.Should().Contain("Acme Staffing");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailTemplates.CountAsync()).Should().Be(0, "preview is stateless and must not create template rows");
    }

    [Fact]
    public async Task Tenant_isolation_hides_other_tenants_templates()
    {
        var tenantAId = await SeedTenantAsync("acme", "Acme Staffing");
        var tenantBId = await SeedTenantAsync("globex", "Globex Talent");

        var recruiterA = RecruiterFor(tenantAId, "auth0|rec-a", "ra@a.example");
        var recruiterB = RecruiterFor(tenantBId, "auth0|rec-b", "rb@b.example");

        // Tenant A creates a template — tenant B must not see it.
        var createA = await recruiterA.PostAsJsonAsync("/api/v1/recruiter/email-templates", new
        {
            slug = "interview-invite",
            name = "Interview Invite",
            subject = "Hello",
            bodyMarkdown = "Body",
        });
        createA.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdA = await createA.Content.ReadFromJsonAsync<EmailTemplateResponse>();

        var listB = await recruiterB.GetAsync("/api/v1/recruiter/email-templates");
        var bodyB = await listB.Content.ReadFromJsonAsync<EmailTemplateListResponse>();
        bodyB!.Items.Should().BeEmpty("the tenant filter must hide templates owned by another tenant");

        var detailB = await recruiterB.GetAsync($"/api/v1/recruiter/email-templates/{createdA!.Id}");
        detailB.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "the row is invisible to tenant B's filter, so the lookup misses and the endpoint 404s — never returns the row");
    }

    [Fact]
    public async Task Duplicate_slug_within_a_tenant_returns_409()
    {
        var tenantId = await SeedTenantAsync("acme", "Acme Staffing");
        var recruiter = RecruiterFor(tenantId, "auth0|rec-a", "ra@a.example");

        var first = await recruiter.PostAsJsonAsync("/api/v1/recruiter/email-templates", new
        {
            slug = "interview-invite",
            name = "Interview Invite",
            subject = "Hello",
            bodyMarkdown = "Body",
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await recruiter.PostAsJsonAsync("/api/v1/recruiter/email-templates", new
        {
            slug = "interview-invite",
            name = "Different display name",
            subject = "Different subject",
            bodyMarkdown = "Different body",
        });
        second.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            "the (tenant_id, slug) unique index must be surfaced as 409, not a 500");
    }

    [Fact]
    public async Task Same_slug_is_allowed_across_different_tenants()
    {
        var tenantAId = await SeedTenantAsync("acme", "Acme Staffing");
        var tenantBId = await SeedTenantAsync("globex", "Globex Talent");

        var recruiterA = RecruiterFor(tenantAId, "auth0|rec-a", "ra@a.example");
        var recruiterB = RecruiterFor(tenantBId, "auth0|rec-b", "rb@b.example");

        var createA = await recruiterA.PostAsJsonAsync("/api/v1/recruiter/email-templates", new
        {
            slug = "interview-invite",
            name = "Interview Invite",
            subject = "A subject",
            bodyMarkdown = "A body",
        });
        createA.StatusCode.Should().Be(HttpStatusCode.Created);

        var createB = await recruiterB.PostAsJsonAsync("/api/v1/recruiter/email-templates", new
        {
            slug = "interview-invite",
            name = "Interview Invite",
            subject = "B subject",
            bodyMarkdown = "B body",
        });
        createB.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "the unique index is composite on (tenant_id, slug) — different tenants can both own the same slug");
    }

    [Fact]
    public async Task Non_recruiter_caller_gets_403_on_POST()
    {
        var tenantId = await SeedTenantAsync("acme", "Acme Staffing");

        var candidate = _factory
            .WithAuthenticatedUser(tenantId, "auth0|candidate", "c@example.com", Roles.Candidate)
            .CreateClient();

        var response = await candidate.PostAsJsonAsync("/api/v1/recruiter/email-templates", new
        {
            slug = "interview-invite",
            name = "Interview Invite",
            subject = "Hello",
            bodyMarkdown = "Body",
        });

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the recruiter-access policy must reject candidates even though they're authenticated");
    }

    private HttpClient RecruiterFor(Guid tenantId, string subject, string email) =>
        _factory
            .WithAuthenticatedUser(tenantId, subject, email, Roles.Recruiter)
            .CreateClient();

    private async Task<Guid> SeedTenantAsync(string slug, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant(slug, name);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }
}
