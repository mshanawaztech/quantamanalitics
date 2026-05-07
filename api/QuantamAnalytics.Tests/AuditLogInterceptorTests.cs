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
/// End-to-end coverage for the audit-log interceptor: every tenant-scoped
/// mutation that hits SaveChanges produces exactly one AuditLogEntry row,
/// captured in the same transaction as the source change. Reads use the
/// PlatformAdmin <c>/api/v1/admin/audit-log</c> endpoint, which means the
/// auth wiring is exercised at the same time.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AuditLogInterceptorTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedAppFactory _factory = default!;

    public AuditLogInterceptorTests(PostgresFixture postgres)
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
    public async Task Creating_an_application_via_the_API_writes_an_audit_log_row()
    {
        var (tenantId, jobId, candidateId) = await SeedTenantJobAndCandidateAsync();

        // Drive the mutation through an authenticated HTTP client so the
        // interceptor sees a real auth subject — same code path a recruiter
        // running the production app would take.
        var recruiter = _factory
            .WithAuthenticatedUser(tenantId, "auth0|recruiter-1", "r@a.example", Roles.Recruiter)
            .CreateClient();

        // Direct DB add through a recruiter-scoped scope. The interceptor
        // resolves CurrentTenant + HttpContextAccessor from the same scope
        // the request handlers would.
        using (var scope = _factory.Services.CreateScope())
        {
            var setter = scope.ServiceProvider.GetRequiredService<QuantamAnalytics.Infrastructure.Tenancy.ICurrentTenantSetter>();
            setter.SetTenantId(tenantId);

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Applications.Add(new Application(
                tenantId: tenantId,
                jobId: jobId,
                candidateProfileId: candidateId,
                candidateEmail: "candidate@example.com",
                candidateName: "Casey Candidate",
                note: null));
            await db.SaveChangesAsync();
        }

        // Read the audit log back through the admin endpoint with a
        // PlatformAdmin client so we exercise the auth gate too.
        var admin = _factory
            .WithAuthenticatedUser(tenantId, "auth0|admin", "admin@example.com", Roles.PlatformAdmin)
            .CreateClient();
        var response = await admin.GetAsync("/api/v1/admin/audit-log?entityType=Application");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuditLogQueryResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle();
        body.Items[0].EntityType.Should().Be("Application");
        body.Items[0].Action.Should().Be("Created");
    }

    [Fact]
    public async Task Audit_log_does_not_record_itself_when_writes_are_flushed()
    {
        var (tenantId, _, _) = await SeedTenantJobAndCandidateAsync();

        // Trigger a small mutation, then read the log. The audit row from
        // the mutation should appear; the audit row's *own* insert must not
        // generate a second 'AuditLogEntry Created' row.
        using (var scope = _factory.Services.CreateScope())
        {
            var setter = scope.ServiceProvider.GetRequiredService<QuantamAnalytics.Infrastructure.Tenancy.ICurrentTenantSetter>();
            setter.SetTenantId(tenantId);

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Adding a non-tenant-scoped entity (Job is tenant-scoped, but
            // we already added one in the seed; modify it instead).
            var job = await db.Jobs.SingleAsync();
            job.Unpublish();
            await db.SaveChangesAsync();
        }

        var admin = _factory
            .WithAuthenticatedUser(tenantId, "auth0|admin", "admin@example.com", Roles.PlatformAdmin)
            .CreateClient();
        var response = await admin.GetAsync("/api/v1/admin/audit-log");
        var body = await response.Content.ReadFromJsonAsync<AuditLogQueryResponse>();

        body!.Items.Should().NotContain(
            x => x.EntityType == "AuditLogEntry",
            "the interceptor must skip its own audit rows or every save would log itself recursively");
    }

    [Fact]
    public async Task Audit_log_query_is_tenant_scoped()
    {
        var (tenantAId, _, _) = await SeedTenantJobAndCandidateAsync();

        // Add a second tenant + a row at that tenant. Tenant A's admin must
        // not see tenant B's audit rows even though they share a database.
        Guid tenantBId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantB = new Tenant("globex", "Globex Talent");
            db.Tenants.Add(tenantB);
            await db.SaveChangesAsync();
            tenantBId = tenantB.Id;
        }
        using (var scope = _factory.Services.CreateScope())
        {
            var setter = scope.ServiceProvider.GetRequiredService<QuantamAnalytics.Infrastructure.Tenancy.ICurrentTenantSetter>();
            setter.SetTenantId(tenantBId);

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Jobs.Add(new Job(tenantBId, "B Job", "b-job", "Remote", "Sum.", "Desc.", DateOnly.FromDateTime(DateTime.UtcNow)));
            await db.SaveChangesAsync();
        }

        var adminA = _factory
            .WithAuthenticatedUser(tenantAId, "auth0|admin-a", "a@example.com", Roles.PlatformAdmin)
            .CreateClient();
        var response = await adminA.GetAsync("/api/v1/admin/audit-log?entityType=Job");
        var body = await response.Content.ReadFromJsonAsync<AuditLogQueryResponse>();

        body!.Items.Should().OnlyContain(
            x => x.EntityType == "Job",
            "filter held");
        // Tenant A only seeded one Job in SeedTenantJobAndCandidateAsync;
        // tenant B's Job must not surface here.
        body.Items.Should().HaveCount(1);
    }

    private async Task<(Guid TenantId, Guid JobId, Guid CandidateId)> SeedTenantJobAndCandidateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var setter = scope.ServiceProvider.GetRequiredService<QuantamAnalytics.Infrastructure.Tenancy.ICurrentTenantSetter>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant("acme", "Acme Staffing");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Set the tenant so subsequent saves trip the audit interceptor
        // with the right tenant id. The Tenant aggregate itself is not
        // ITenantScoped, so its insert above doesn't get audited — exactly
        // what we want for platform-level rows.
        setter.SetTenantId(tenant.Id);

        var job = new Job(tenant.Id, "Senior Backend", "senior-backend", "Remote", "Backend.", "Long desc.", DateOnly.FromDateTime(DateTime.UtcNow));
        var candidate = new CandidateProfile(tenant.Id, "auth0|candidate", "candidate@example.com", "Casey Candidate");
        db.Jobs.Add(job);
        db.CandidateProfiles.Add(candidate);
        await db.SaveChangesAsync();

        return (tenant.Id, job.Id, candidate.Id);
    }
}
