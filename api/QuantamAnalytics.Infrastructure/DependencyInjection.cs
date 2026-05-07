using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuantamAnalytics.Infrastructure.BackgroundChecks;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Interviews;
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

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(PostgresConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fail fast in dev: a missing connection string is almost always a
            // forgotten `dotnet user-secrets set ConnectionStrings:Postgres`.
            throw new InvalidOperationException(
                $"Connection string '{PostgresConnectionStringName}' is not configured. " +
                "In dev, run: " +
                "dotnet user-secrets set \"ConnectionStrings:Postgres\" \"<your-neon-conn-string>\" " +
                "--project api/QuantamAnalytics.Api");
        }

        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ICurrentTenantSetter>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddSingleton<IInterviewCalendarProviderCatalog, InterviewCalendarProviderCatalog>();
        services.AddSingleton<ICheckrClient, StubCheckrClient>();

        RegisterResumeStorage(services, configuration);

        // Non-pooled context because tenant state is request-scoped. Reusing a
        // pooled DbContext across requests risks stale TenantId leaking into the
        // global query filter. Snake_case naming converts PascalCase model names
        // to postgres conventions (Tenant -> tenants, CreatedAtUtc -> created_at_utc).
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        // /ready endpoint pings this. Returns Healthy only when EF can open
        // a connection and execute a trivial query.
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
}
