using QuantamAnalytics.Api.Endpoints;
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

// EF Core, Postgres, health checks. Reads ConnectionStrings:Postgres from
// configuration (appsettings, user-secrets in dev, env vars in prod).
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Standardized RFC 7807 error responses for any unhandled exception.
app.UseExceptionHandler();
app.UseStatusCodePages();

// CORS unconditionally — the allow-list is the gate, not the environment.
// A wide-open list in dev is intentional; prod gets a narrow list via env.
app.UseCors(DefaultCorsPolicy);

// Liveness — process is up. No DB, no auth, never blocks.
app.MapHealthEndpoint();

// Readiness — process is up AND can reach Postgres. Deployment / load-balancer
// gate. Wired up by AddInfrastructure() via AddDbContextCheck<AppDbContext>.
app.MapHealthChecks("/ready");

app.Run();

// Exposed for WebApplicationFactory<TEntryPoint> in QuantamAnalytics.Tests.
public partial class Program;
