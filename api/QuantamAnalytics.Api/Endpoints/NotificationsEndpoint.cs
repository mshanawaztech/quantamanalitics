using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// In-app notification feed for the current user. Recipients are matched by
/// <c>ICurrentUser.AuthSubject</c>; tenant scope is enforced by the global
/// EF query filter. Open to any authenticated user — every portal needs
/// the bell-icon dropdown.
/// </summary>
public static class NotificationsEndpoint
{
    private const int MaxFeedRows = 50;

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/me/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapPost("/{id:guid}/read", MarkReadAsync);
        group.MapPost("/read-all", MarkAllReadAsync);

        return app;
    }

    private static async Task<Results<Ok<NotificationFeedResponse>, ProblemHttpResult>> ListAsync(
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

        var rows = await db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientAuthSubject == subject)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(MaxFeedRows)
            .Select(n => new NotificationResponse(
                n.Id,
                n.Kind,
                n.Title,
                n.Body,
                n.TargetUrl,
                n.CreatedAtUtc,
                n.ReadAtUtc))
            .ToArrayAsync(cancellationToken);

        var unreadCount = await db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientAuthSubject == subject && n.ReadAtUtc == null)
            .CountAsync(cancellationToken);

        return TypedResults.Ok(new NotificationFeedResponse(rows, unreadCount));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> MarkReadAsync(
        Guid id,
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

        // Filter by recipient as well as id — a user must not be able to
        // flip another user's notifications read even within the same tenant.
        var notification = await db.Notifications
            .FirstOrDefaultAsync(
                n => n.Id == id && n.RecipientAuthSubject == subject,
                cancellationToken);
        if (notification is null)
        {
            return TypedResults.NotFound();
        }

        notification.MarkRead();
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<MarkAllReadResponse>, ProblemHttpResult>> MarkAllReadAsync(
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
        var nowUtc = DateTimeOffset.UtcNow;

        var affected = await db.Notifications
            .Where(n => n.RecipientAuthSubject == subject && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(
                set => set.SetProperty(n => n.ReadAtUtc, nowUtc),
                cancellationToken);

        return TypedResults.Ok(new MarkAllReadResponse(affected, nowUtc));
    }

    private static ProblemHttpResult SubjectRequired() =>
        TypedResults.Problem(
            title: "Authenticated tenant subject required",
            detail: "The notifications feed requires a tenant_id claim and an authenticated subject.",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record NotificationFeedResponse(
    NotificationResponse[] Items,
    int UnreadCount);

public sealed record NotificationResponse(
    Guid Id,
    string Kind,
    string Title,
    string Body,
    string? TargetUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);

public sealed record MarkAllReadResponse(int MarkedRead, DateTimeOffset At);
