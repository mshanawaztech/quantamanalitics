namespace QuantamAnalytics.Infrastructure.Tenancy;

internal sealed class CurrentUser : ICurrentUser, ICurrentUserSetter
{
    public string? AuthSubject { get; private set; }

    public void SetAuthSubject(string? authSubject)
    {
        AuthSubject = string.IsNullOrWhiteSpace(authSubject) ? null : authSubject.Trim();
    }
}
