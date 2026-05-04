using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Tests;

public sealed class AppDbContextTenantFilterTests
{
    [Fact]
    public async Task Tenant_scoped_queries_only_return_rows_for_current_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var context = NewContext(tenantA);
        context.Records.AddRange(
            new TestTenantRecord(Guid.CreateVersion7(), tenantA, "Tenant A"),
            new TestTenantRecord(Guid.CreateVersion7(), tenantB, "Tenant B"));
        await context.SaveChangesAsync();

        var visibleRows = await context.Records
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToArrayAsync();

        visibleRows.Should().Equal("Tenant A");
    }

    [Fact]
    public async Task Tenant_scoped_queries_return_no_rows_when_no_tenant_is_resolved()
    {
        var tenantId = Guid.NewGuid();

        await using var seededContext = NewContext(tenantId, "shared-db");
        seededContext.Records.Add(new TestTenantRecord(Guid.CreateVersion7(), tenantId, "Seeded"));
        await seededContext.SaveChangesAsync();

        await using var anonymousContext = NewContext(null, "shared-db");
        var visibleRows = await anonymousContext.Records.ToArrayAsync();

        visibleRows.Should().BeEmpty();
    }

    [Fact]
    public async Task Tenant_aggregate_itself_is_not_filtered()
    {
        await using var context = NewContext(null);

        var tenantEntity = context.Model.FindEntityType(typeof(QuantamAnalytics.Domain.Entities.Tenant));

        tenantEntity.Should().NotBeNull();
        tenantEntity!.GetDeclaredQueryFilters().Should().BeEmpty();
    }

    private static TenantFilterTestDbContext NewContext(Guid? tenantId, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TenantFilterTestDbContext(options, new TestCurrentTenant(tenantId));
    }

    private sealed class TenantFilterTestDbContext : AppDbContext
    {
        public TenantFilterTestDbContext(
            DbContextOptions<AppDbContext> options,
            ICurrentTenant currentTenant) : base(options, currentTenant)
        {
        }

        public DbSet<TestTenantRecord> Records => Set<TestTenantRecord>();

        protected override void ConfigureAdditionalModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestTenantRecord>(ConfigureRecord);
        }

        private static void ConfigureRecord(EntityTypeBuilder<TestTenantRecord> builder)
        {
            builder.ToTable("test_tenant_records");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
        }
    }

    private sealed record TestTenantRecord(Guid Id, Guid TenantId, string Name) : ITenantScoped;

    private sealed class TestCurrentTenant(Guid? tenantId) : ICurrentTenant
    {
        public Guid? TenantId { get; } = tenantId;
    }
}
