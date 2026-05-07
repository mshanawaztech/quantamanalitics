namespace QuantamAnalytics.Infrastructure.Tenancy;

/// <summary>
/// Request-scoped view of the authenticated user's subject (Auth0 <c>sub</c>
/// claim — usually <c>auth0|...</c>). Null means the request is anonymous
/// or no subject claim was found on the principal.
/// </summary>
/// <remarks>
/// Mirrors <see cref="ICurrentTenant"/> intentionally. Both come from the
/// same JWT, both are populated by the same middleware in the API project,
/// both flow into Infrastructure consumers (e.g. the audit-log interceptor)
/// without that layer needing a dependency on ASP.NET Core types.
/// </remarks>
public interface ICurrentUser
{
    string? AuthSubject { get; }
}

/// <summary>
/// Internal write-side contract used by middleware to populate the auth
/// subject for the current request before downstream services run.
/// </summary>
public interface ICurrentUserSetter
{
    void SetAuthSubject(string? authSubject);
}
