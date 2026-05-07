using Microsoft.Extensions.Options;
using QuantamAnalytics.Api;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Api.Demo;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Api.Tenancy;
using QuantamAnalytics.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Allowed CORS origins — comma-separated list read from configuration.
//   Local dev: appsettings.Development.json (or fall back to localhost:4200)
//   Cloud dev: bicep injects the SWA URL via env var Cors__AllowedOrigins
//   Prod:      env var with the prod hostname(s)
const string DefaultCorsPolicy = "DefaultCors";
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:4200")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy(DefaultCorsPolicy, policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddProblemDetails();
builder.Services.Configure<DemoDataOptions>(builder.Configuration.GetSection("DemoData"));

// EF Core, Postgres, health checks. Reads ConnectionStrings:Postgres from
// configuration (appsettings, user-secrets in dev, env vars in prod).
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<DemoDataSeeder>();

// JWT bearer + role policies. Returns false if Auth0:Domain / Auth0:Audience
// aren't configured, in which case auth middleware is also skipped below.
// Lets the API run unauthenticated in pre-Auth0 environments without crashing.
var authEnabled = builder.Services.AddPlatformAuth(builder.Configuration);

var app = builder.Build();

if (!authEnabled)
{
    app.Logger.Auth0NotConfigured(
        AuthExtensions.Auth0DomainKey,
        AuthExtensions.Auth0AudienceKey);
}

await SeedDemoDataIfEnabledAsync(app);

// Standardized RFC 7807 error responses for any unhandled exception.
app.UseExceptionHandler();
app.UseStatusCodePages();

// CORS unconditionally — the allow-list is the gate, not the environment.
// A wide-open list in dev is intentional; prod gets a narrow list via env.
app.UseCors(DefaultCorsPolicy);

if (authEnabled)
{
    app.UseAuthentication();
    app.UseTenantResolution();
    app.UseAuthorization();
}

// Liveness — process is up. No DB, no auth, never blocks.
// .AllowAnonymous() is set inside MapHealthEndpoint().
app.MapHealthEndpoint();
app.MapPublicJobsEndpoints();
app.MapJobFeedEndpoints();

// Readiness — process is up AND can reach Postgres. Anonymous on purpose:
// load balancer probes can't carry a JWT.
app.MapHealthChecks("/ready").AllowAnonymous();

// Authenticated user info. Only routed when auth is wired — otherwise
// RequireAuthorization() with no auth scheme would fail at request time.
if (authEnabled)
{
    app.MapInterviewSchedulingEndpoints();
    app.MapBackgroundCheckEndpoints();
    app.MapEsignDocumentEndpoints();
    app.MapOnboardingChecklistEndpoints();
    app.MapMeEndpoint();
    app.MapClientApprovalEndpoints();
    app.MapClientPortalDocumentsEndpoints();
    app.MapContractorTimesheetEndpoints();
    app.MapCandidateProfileEndpoints();
    app.MapRecruiterPortalEndpoints();
    app.MapReportingEndpoints();
}

app.Run();

static async Task SeedDemoDataIfEnabledAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var options = scope.ServiceProvider.GetRequiredService<IOptions<DemoDataOptions>>().Value;
    if (!options.SeedOnStartup)
    {
        return;
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}

// Exposed for WebApplicationFactory<TEntryPoint> in QuantamAnalytics.Tests.
public partial class Program;
