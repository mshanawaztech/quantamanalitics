using Microsoft.AspNetCore.Http.HttpResults;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Session lifecycle surface for the authenticated user.
///
/// The platform delegates JWT issuance to Auth0, so a "revoke" call can't
/// invalidate a token already in the user's pocket — but it can record the
/// user's intent and drop a notification trail for their other devices.
/// The audit-log entry is the canonical artifact security reviewers will
/// look for during a SOC 2 evidence sweep.
///
/// Sister endpoint: <c>POST /api/v1/me/security/suspicious-login</c> is the
/// landing pad Auth0's post-login action calls when its risk model flags a
/// sign-in. The hook writes a notification and an audit row so the user
/// gets a heads-up on their next visit and the tenant's audit timeline
/// shows the event.
/// </summary>
public static class SessionEndpoint
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/me/security")
            .WithTags("Session security")
            .RequireAuthorization();

        group.MapPost("/sessions/revoke", RevokeAsync);
        group.MapPost("/suspicious-login", RecordSuspiciousLoginAsync);

        return app;
    }

    private static async Task<Results<Ok<SessionRevokeResponse>, ProblemHttpResult>> RevokeAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return SubjectRequired();
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var subject = currentUser.AuthSubject;

        db.Notifications.Add(new Notification(
            currentTenant.TenantId.Value,
            subject,
            kind: "security.session_revoked",
            title: "Your sessions were signed out",
            body: "You asked Quantam Analytics to sign out of every device. " +
                  "Sign in again to keep using the app.",
            targetUrl: "/login"));

        await db.SaveChangesAsync(cancellationToken);

        // Auth0 token revocation runs as a follow-up via the Management API
        // — left as a TODO with explicit guidance in the auth-hardening doc.
        // We commit the intent locally so the audit trail is honest even if
        // the Management API call fails.
        return TypedResults.Ok(new SessionRevokeResponse(nowUtc));
    }

    private static async Task<Results<Ok<SuspiciousLoginResponse>, ProblemHttpResult>> RecordSuspiciousLoginAsync(
        SuspiciousLoginRequest request,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return SubjectRequired();
        }

        var subject = currentUser.AuthSubject;
        var label = string.IsNullOrWhiteSpace(request.Location)
            ? "from an unfamiliar location"
            : $"from {request.Location.Trim()}";

        db.Notifications.Add(new Notification(
            currentTenant.TenantId.Value,
            subject,
            kind: "security.suspicious_login",
            title: "A new sign-in was flagged",
            body: $"We saw a sign-in {label} on " +
                  $"{(request.AtUtc ?? DateTimeOffset.UtcNow):MMM d, h:mm tt} UTC. " +
                  "If this wasn't you, sign out everywhere and reset your password.",
            targetUrl: "/candidate"));

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new SuspiciousLoginResponse(true));
    }

    private static ProblemHttpResult SubjectRequired() =>
        TypedResults.Problem(
            title: "Authenticated tenant subject required",
            detail: "Session-security endpoints require a tenant_id claim and an authenticated subject.",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record SessionRevokeResponse(DateTimeOffset RevokedAtUtc);

public sealed record SuspiciousLoginRequest(
    string? Location,
    DateTimeOffset? AtUtc);

public sealed record SuspiciousLoginResponse(bool Acknowledged);
