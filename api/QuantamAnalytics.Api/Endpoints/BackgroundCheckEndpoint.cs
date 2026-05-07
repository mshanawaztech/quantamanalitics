using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.BackgroundChecks;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

public static class BackgroundCheckEndpoint
{
    public static IEndpointRouteBuilder MapBackgroundCheckEndpoints(this IEndpointRouteBuilder app)
    {
        var recruiterGroup = app.MapGroup("/api/v1/background-checks")
            .WithTags("Background Checks")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        recruiterGroup.MapGet("/", ListAsync);
        recruiterGroup.MapPost("/", RequestAsync);

        // Webhook is anonymous because Checkr calls it directly — but in
        // Phase 5 we'll require an HMAC header signed with the per-tenant
        // webhook secret. For now the endpoint accepts the raw payload and
        // applies the terminal status to a pre-existing row.
        app.MapPost("/api/v1/background-checks/webhooks/checkr", WebhookAsync)
            .WithTags("Background Checks")
            .AllowAnonymous();

        return app;
    }

    private static async Task<Results<Ok<BackgroundCheckListResponse>, ProblemHttpResult>> ListAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Background check listing requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var rows = await db.BackgroundChecks
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => new BackgroundCheckResponse(
                x.Id,
                x.CandidateProfileId,
                x.CandidateName,
                x.CandidateEmail,
                x.PackageSlug,
                x.ProviderReportId,
                x.Status.ToString(),
                x.StatusDetail,
                x.RequestedAtUtc,
                x.CompletedAtUtc))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new BackgroundCheckListResponse(rows));
    }

    private static async Task<Results<Ok<BackgroundCheckResponse>, NotFound, ProblemHttpResult>> RequestAsync(
        BackgroundCheckRequestBody body,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICheckrClient checkr,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Requesting a background check requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var requestedBy = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            return TypedResults.Problem(
                title: "Authenticated subject missing",
                detail: "Cannot record the requesting recruiter without a subject claim.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // The candidate must already be visible inside the caller's tenant —
        // global query filter on AppDbContext enforces that.
        var profile = await db.CandidateProfiles
            .FirstOrDefaultAsync(x => x.Id == body.CandidateProfileId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        // CandidateProfile.FullName is optional. Fall back to the email's
        // local part so the BackgroundCheck row still has a non-empty
        // candidate name (the entity ctor enforces it).
        var candidateName = string.IsNullOrWhiteSpace(profile.FullName)
            ? profile.Email.Split('@')[0]
            : profile.FullName;

        var check = new BackgroundCheck(
            tenantId: currentTenant.TenantId.Value,
            candidateProfileId: profile.Id,
            candidateEmail: profile.Email,
            candidateName: candidateName,
            requestedByAuthSubject: requestedBy,
            packageSlug: string.IsNullOrWhiteSpace(body.PackageSlug) ? "tasker_standard" : body.PackageSlug);

        db.BackgroundChecks.Add(check);
        await db.SaveChangesAsync(cancellationToken);

        var checkrResult = await checkr.RequestReportAsync(check, cancellationToken);
        check.AttachProviderReport(checkrResult.ProviderReportId);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new BackgroundCheckResponse(
            check.Id,
            check.CandidateProfileId,
            check.CandidateName,
            check.CandidateEmail,
            check.PackageSlug,
            check.ProviderReportId,
            check.Status.ToString(),
            check.StatusDetail,
            check.RequestedAtUtc,
            check.CompletedAtUtc));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> WebhookAsync(
        CheckrWebhookPayload payload,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.ReportId))
        {
            return TypedResults.Problem(
                title: "Missing report id",
                detail: "Checkr webhook payload must include the report id.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var status = MapStatus(payload.Status);
        if (status is null)
        {
            return TypedResults.Problem(
                title: "Unsupported status",
                detail: $"Unknown Checkr status '{payload.Status}'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Webhook bypasses the tenant filter — Checkr doesn't know our tenants.
        // We look up by provider id (unique) and trust the persisted TenantId
        // on the row to keep the data tenant-scoped.
        var check = await db.BackgroundChecks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.ProviderReportId == payload.ReportId, cancellationToken);

        if (check is null)
        {
            return TypedResults.NotFound();
        }

        check.RecordTerminalStatus(status.Value, payload.Detail);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static BackgroundCheckStatus? MapStatus(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "clear" => BackgroundCheckStatus.Clear,
            "consider" => BackgroundCheckStatus.Consider,
            "cancelled" or "canceled" => BackgroundCheckStatus.Cancelled,
            _ => null,
        };
}

public sealed record BackgroundCheckRequestBody(
    Guid CandidateProfileId,
    string? PackageSlug);

public sealed record BackgroundCheckListResponse(
    BackgroundCheckResponse[] Items);

public sealed record BackgroundCheckResponse(
    Guid Id,
    Guid CandidateProfileId,
    string CandidateName,
    string CandidateEmail,
    string PackageSlug,
    string? ProviderReportId,
    string Status,
    string? StatusDetail,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? CompletedAtUtc);

/// <summary>
/// Minimal projection of the Checkr report.completed webhook body. Real
/// Checkr payloads carry many more fields; we accept what we need and
/// ignore the rest, which is the documented forward-compatibility pattern
/// they recommend.
/// </summary>
public sealed record CheckrWebhookPayload(
    string ReportId,
    string Status,
    string? Detail);
