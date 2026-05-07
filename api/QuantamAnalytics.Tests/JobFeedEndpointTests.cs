using System.Net;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Tests.Fixtures;

namespace QuantamAnalytics.Tests;

/// <summary>
/// End-to-end coverage for the Indeed job feed.
///
/// The feed is anonymous, parameterized by tenant slug, and crosses the
/// global query filter via <c>IgnoreQueryFilters()</c>. That combo is the
/// exact place a sloppy refactor could leak rows across tenants — so the
/// tests here are deliberately the "tenant A's slug returns only tenant A's
/// jobs" shape rather than only checking XML schema.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class JobFeedEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public JobFeedEndpointTests(PostgresFixture postgres)
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
    public async Task Feed_returns_only_published_jobs_for_the_requested_tenant()
    {
        await SeedAcmePublishedAndUnpublishedJobsAndAGlobexJobAsync();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/feeds/acme/indeed.xml");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");

        var body = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(body);
        var jobs = doc.Root!.Elements("job").ToArray();

        jobs.Should().ContainSingle("only acme's published job belongs in the feed");
        var titles = jobs.Select(j => (string)j.Element("title")!).ToArray();
        titles.Should().Contain("Senior Backend Engineer");
        titles.Should().NotContain("Acme Draft Role", "unpublished jobs must be excluded");
        titles.Should().NotContain("Globex Frontend Role", "other tenants' jobs must never leak across the feed boundary");

        // Cheap shape check — Indeed's required elements must all be present
        // on every <job>. If we forget one, the feed silently de-indexes.
        var job = jobs[0];
        job.Element("referencenumber").Should().NotBeNull();
        job.Element("url").Should().NotBeNull();
        job.Element("company").Should().NotBeNull();
        job.Element("city").Should().NotBeNull();
        job.Element("country").Should().NotBeNull();
        job.Element("description").Should().NotBeNull();
        job.Element("date").Should().NotBeNull();
    }

    [Fact]
    public async Task Feed_for_unknown_tenant_slug_returns_404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/feeds/does-not-exist/indeed.xml");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Feed_for_inactive_tenant_returns_404()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = new Tenant("inactive-co", "Inactive Co");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Toggle to inactive via raw update — Tenant has no public Deactivate
        // method today, and this test wants to assert the IsActive guard
        // without coupling to whatever the eventual API for that turns into.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE tenants SET is_active = false WHERE id = {tenant.Id}");

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/feeds/inactive-co/indeed.xml");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void BuildIndeedXml_renders_expected_structure_for_a_known_job()
    {
        var tenantId = Guid.CreateVersion7();
        var job = new Job(
            tenantId: tenantId,
            title: "QA Engineer",
            slug: "qa-engineer",
            location: "Austin, TX",
            summary: "QA role.",
            description: "<p>Test plan ownership.</p>",
            postedOnUtc: new DateOnly(2026, 4, 1));

        var xml = JobFeedEndpoint.BuildIndeedXml("Acme Staffing", "https://web.example", new[] { job });

        var doc = XDocument.Parse(xml);
        doc.Declaration?.Encoding.Should().Be("utf-8", "Indeed rejects utf-16 declarations");
        doc.Root!.Name.LocalName.Should().Be("source");
        doc.Root.Element("publisher")!.Value.Should().Be("Acme Staffing");

        var rendered = doc.Root.Element("job")!;
        rendered.Element("title")!.Value.Should().Be("QA Engineer");
        rendered.Element("url")!.Value.Should().Be("https://web.example/jobs/qa-engineer");
        rendered.Element("city")!.Value.Should().Be("Austin, TX");
        rendered.Element("description")!.Value.Should().Contain("Test plan ownership");
    }

    private async Task SeedAcmePublishedAndUnpublishedJobsAndAGlobexJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var acme = new Tenant("acme", "Acme Staffing");
        var globex = new Tenant("globex", "Globex Talent");
        db.Tenants.AddRange(acme, globex);

        var acmePublished = new Job(
            tenantId: acme.Id,
            title: "Senior Backend Engineer",
            slug: "senior-backend-engineer",
            location: "Remote",
            summary: "Backend role.",
            description: "Long description.",
            postedOnUtc: new DateOnly(2026, 4, 1));

        var acmeDraft = new Job(
            tenantId: acme.Id,
            title: "Acme Draft Role",
            slug: "acme-draft-role",
            location: "Remote",
            summary: "Draft.",
            description: "Hidden.",
            postedOnUtc: new DateOnly(2026, 4, 2));
        acmeDraft.Unpublish();

        var globexJob = new Job(
            tenantId: globex.Id,
            title: "Globex Frontend Role",
            slug: "globex-frontend-role",
            location: "Remote",
            summary: "Frontend.",
            description: "Globex only.",
            postedOnUtc: new DateOnly(2026, 4, 3));

        db.Jobs.AddRange(acmePublished, acmeDraft, globexJob);
        await db.SaveChangesAsync();
    }
}
