using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Entities;

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
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Pick up every IEntityTypeConfiguration<T> in this assembly. Lets us
        // keep one configuration class per entity instead of bloating this method.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
