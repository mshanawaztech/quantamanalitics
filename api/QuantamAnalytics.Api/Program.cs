using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// CORS for the Angular dev server (http://localhost:4200). The production
// origin is added via configuration once the SWA URL is known (PR-04).
const string DevCorsPolicy = "DevClient";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
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

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
}

// Liveness — process is up. No DB, no auth, never blocks.
app.MapHealthEndpoint();

// Readiness — process is up AND can reach Postgres. Deployment / load-balancer
// gate. Wired up by AddInfrastructure() via AddDbContextCheck<AppDbContext>.
app.MapHealthChecks("/ready");

app.Run();

// Exposed for WebApplicationFactory<TEntryPoint> in QuantamAnalytics.Tests.
public partial class Program;
