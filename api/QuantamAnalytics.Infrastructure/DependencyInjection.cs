using System.Net.Http.Headers;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuantamAnalytics.Domain.Pdf;
using QuantamAnalytics.Infrastructure.AI;
using QuantamAnalytics.Infrastructure.BackgroundChecks;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Esign;
using QuantamAnalytics.Infrastructure.Features;
using QuantamAnalytics.Infrastructure.Interviews;
using QuantamAnalytics.Infrastructure.JobBoards;
using QuantamAnalytics.Infrastructure.Pdf;
using QuantamAnalytics.Infrastructure.ResumeParsing;
using QuantamAnalytics.Infrastructure.Storage;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Infrastructure;

/// <summary>
/// One entry point for the Api project to wire up everything in this layer.
/// Today: just <see cref="AppDbContext"/>. Tomorrow: R2 client, Auth0 client,
/// Resend client, etc. — all added here so Program.cs stays a thin composition root.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Connection string key in appsettings / user-secrets / env vars.</summary>
    public const string PostgresConnectionStringName = "Postgres";

    /// <summary>
    /// Name used for the in-memory EF Core database when no real Postgres
    /// connection string is configured and we're in a non-Production
    /// environment. Constant so multiple resolutions inside one process
    /// share the same in-memory store.
    /// </summary>
    private const string DevInMemoryDatabaseName = "quantamanalitics_unconfigured";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var connectionString = configuration.GetConnectionString(PostgresConnectionStringName);
        var useInMemoryFallback = string.IsNullOrWhiteSpace(connectionString);

        if (useInMemoryFallback)
        {
            // Production: fail fast. A missing connection string in prod is
            // almost always a deploy misconfig, and falling through to an
            // in-memory DB would silently lose every write instead of
            // crashing on startup where the failure is loud.
            //
            // Non-production (Development, Staging, integration-test hosts):
            // fall back to EF Core's in-memory provider so the host can
            // build for unit / integration tests and dev scenarios that
            // don't need real Postgres. Queries return empty results;
            // endpoints exercising auth / validation / status-code paths
            // (e.g. tenant-claim precondition checks) still produce their
            // intended response codes instead of 500s from a timed-out
            // Npgsql connect.
            //
            // We resolve the environment via IHostEnvironment (set by
            // WebApplicationFactory and CreateBuilder), NOT via
            // configuration[ASPNETCORE_ENVIRONMENT], because the test host
            // sets the IHostEnvironment property directly without touching
            // the env var.
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    $"Connection string '{PostgresConnectionStringName}' is not configured. " +
                    "In dev, run: " +
                    "dotnet user-secrets set \"ConnectionStrings:Postgres\" \"<your-neon-conn-string>\" " +
                    "--project api/QuantamAnalytics.Api");
            }
        }

        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ICurrentTenantSetter>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<ICurrentUserSetter>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddSingleton<IInterviewCalendarProviderCatalog, InterviewCalendarProviderCatalog>();
        services.AddSingleton<IMeetingLinkGenerator, MeetingLinkGenerator>();
        services.AddSingleton<ICheckrClient, StubCheckrClient>();
        services.AddSingleton<IDocuSealClient, StubDocuSealClient>();
        services.AddSingleton<IDicePostingClient, StubDicePostingClient>();
        services.AddSingleton<IResumeParser, StubResumeParser>();
        services.AddSingleton<ICandidateMatcher, StubCandidateMatcher>();
        AddCopilot(services, configuration);
        services.AddSingleton<IFeatureGate, AppSettingsFeatureGate>();
        services.AddSingleton<IInvoicePdfRenderer, QuestPdfInvoiceRenderer>();

        // Audit-log interceptor — scoped so it sees the per-request tenant
        // and auth subject. Resolved into the DbContext options below via
        // the (sp, options) overload of AddDbContext.
        services.AddScoped<AuditLogSaveChangesInterceptor>();

        RegisterResumeStorage(services, configuration);

        // Non-pooled context because tenant state is request-scoped. Reusing a
        // pooled DbContext across requests risks stale TenantId leaking into the
        // global query filter. Snake_case naming converts PascalCase model names
        // to postgres conventions (Tenant -> tenants, CreatedAtUtc -> created_at_utc).
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            if (useInMemoryFallback)
            {
                // No real connection string + non-Production: use EF's
                // in-memory provider. Snake_case + interceptors aren't
                // applicable here; the in-memory provider has no SQL to
                // shape and no SaveChanges hooks beyond what EF runs
                // natively. This is strictly a "let the host start"
                // fallback for tests / dev — production fails fast above.
                options.UseInMemoryDatabase(DevInMemoryDatabaseName);
                return;
            }

            options
                .UseNpgsql(connectionString, npgsql => npgsql
                    .MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention()
                // Interceptor is scoped, same as DbContext — resolve from the
                // current request's service provider so it sees the per-request
                // tenant + auth subject rather than a stale snapshot.
                .AddInterceptors(sp.GetRequiredService<AuditLogSaveChangesInterceptor>());
        });

        // /ready endpoint pings this. Returns Healthy only when EF can open
        // a connection and execute a trivial query. The in-memory provider
        // always reports Healthy, which is what we want for the no-config
        // fallback path — readiness in tests shouldn't depend on Postgres.
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(name: "postgres");

        return services;
    }

    private static void RegisterResumeStorage(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var accountId = configuration["Storage:R2:AccountId"];
        var accessKeyId = configuration["Storage:R2:AccessKeyId"];
        var secretAccessKey = configuration["Storage:R2:SecretAccessKey"];
        var bucketName = configuration["Storage:R2:Bucket"];

        if (string.IsNullOrWhiteSpace(accountId) ||
            string.IsNullOrWhiteSpace(accessKeyId) ||
            string.IsNullOrWhiteSpace(secretAccessKey) ||
            string.IsNullOrWhiteSpace(bucketName))
        {
            services.AddSingleton<IResumeStorage, DisabledResumeStorage>();
            return;
        }

        services.AddSingleton<IAmazonS3>(_ =>
        {
            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
            return new AmazonS3Client(credentials, new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true
            });
        });
        services.AddSingleton<IResumeStorage>(_ =>
            new R2ResumeStorage(_.GetRequiredService<IAmazonS3>(), bucketName));
    }

    /// <summary>
    /// Registers the copilot. With a Groq API key configured, the real
    /// Groq-backed provider runs with the deterministic stub as its fallback;
    /// otherwise the stub serves directly. Config keys: <c>Groq:ApiKey</c>
    /// and optional <c>Groq:Model</c> (default llama-3.3-70b-versatile).
    /// </summary>
    private static void AddCopilot(IServiceCollection services, IConfiguration configuration)
    {
        var apiKey = configuration["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton<ICopilotProvider, StubCopilotProvider>();
            return;
        }

        var model = configuration["Groq:Model"];
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "llama-3.3-70b-versatile";
        }

        services.AddSingleton<StubCopilotProvider>();
        services.AddSingleton<ICopilotProvider>(sp =>
        {
            var http = new HttpClient
            {
                BaseAddress = new Uri("https://api.groq.com/openai/v1/"),
                Timeout = TimeSpan.FromSeconds(30),
            };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return new GroqCopilotProvider(http, model, sp.GetRequiredService<StubCopilotProvider>());
        });
    }
}
