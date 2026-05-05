namespace QuantamAnalytics.Infrastructure.Tenancy;

internal sealed class CurrentTenant : ICurrentTenant, ICurrentTenantSetter
{
    public Guid? TenantId { get; private set; }

    public void SetTenantId(Guid? tenantId)
    {
        TenantId = tenantId;
    }
}
