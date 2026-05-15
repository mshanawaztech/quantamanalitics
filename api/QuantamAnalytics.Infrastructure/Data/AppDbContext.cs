using System.Reflection;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Infrastructure.Data;

/// <summary>
/// EF Core context for the application database. One per request via the DI
/// container. Snake_case naming is wired up at registration time
/// (see <see cref="DependencyInjection.AddInfrastructure"/>) so generated
/// SQL matches Postgres conventions.
/// </summary>
/// <remarks>
/// PR-07 will layer a tenant resolver and a global query filter on top of
/// this context to enforce multi-tenant isolation on every read.
/// </remarks>
public class AppDbContext : DbContext
{
    private readonly ICurrentTenant _currentTenant;

    internal Guid? CurrentTenantId => _currentTenant.TenantId;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentTenant currentTenant) : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicationTag> ApplicationTags => Set<ApplicationTag>();
    public DbSet<ApplicationTimelineEvent> ApplicationTimelineEvents => Set<ApplicationTimelineEvent>();
    public DbSet<RecruiterApplicationFilterPreset> RecruiterApplicationFilterPresets => Set<RecruiterApplicationFilterPreset>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<InterviewEvent> InterviewEvents => Set<InterviewEvent>();
    public DbSet<BackgroundCheck> BackgroundChecks => Set<BackgroundCheck>();
    public DbSet<EsignDocument> EsignDocuments => Set<EsignDocument>();
    public DbSet<OnboardingChecklistItem> OnboardingChecklistItems => Set<OnboardingChecklistItem>();
    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Pick up every IEntityTypeConfiguration<T> in this assembly. Lets us
        // keep one configuration class per entity instead of bloating this method.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ConfigureAdditionalModel(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Test hook for adding extra entity types to a derived DbContext while
    /// still reusing the production tenant-filter plumbing.
    /// </summary>
    protected virtual void ConfigureAdditionalModel(ModelBuilder modelBuilder)
    {
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(ITenantScoped).IsAssignableFrom(clrType))
            {
                continue;
            }

            var method = typeof(AppDbContext)
                .GetMethod(nameof(SetTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(clrType);

            method.Invoke(this, [modelBuilder]);
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity =>
                (Guid?)entity.TenantId == CurrentTenantId);
    }
}
