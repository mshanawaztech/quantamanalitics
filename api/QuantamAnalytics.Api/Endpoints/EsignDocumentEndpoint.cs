using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Esign;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

public static class EsignDocumentEndpoint
{
    public static IEndpointRouteBuilder MapEsignDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var recruiterGroup = app.MapGroup("/api/v1/esign-documents")
            .WithTags("E-Sign Documents")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        recruiterGroup.MapGet("/", ListAsync);
        recruiterGroup.MapPost("/", SendAsync);
        recruiterGroup.MapPost("/{id:guid}/cancel", CancelAsync);

        // DocuSeal hits us back with view/sign events. Anonymous for now;
        // Phase 5 wraps with an HMAC signature check using a per-tenant
        // webhook secret.
        app.MapPost("/api/v1/esign-documents/webhooks/docuseal", WebhookAsync)
            .WithTags("E-Sign Documents")
            .AllowAnonymous();

        return app;
    }

    private static async Task<Results<Ok<EsignDocumentListResponse>, ProblemHttpResult>> ListAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "E-sign listing requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var rows = await db.EsignDocuments
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => Project(x))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new EsignDocumentListResponse(rows));
    }

    private static async Task<Results<Ok<EsignDocumentResponse>, NotFound, ProblemHttpResult>> SendAsync(
        EsignDocumentSendBody body,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        IDocuSealClient docuSeal,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Sending an e-sign document requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var sentBy = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sentBy))
        {
            return TypedResults.Problem(
                title: "Authenticated subject missing",
                detail: "Cannot record the sending recruiter without a subject claim.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var profile = await db.CandidateProfiles
            .FirstOrDefaultAsync(x => x.Id == body.CandidateProfileId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        var candidateName = string.IsNullOrWhiteSpace(profile.FullName)
            ? profile.Email.Split('@')[0]
            : profile.FullName;

        var doc = new EsignDocument(
            tenantId: currentTenant.TenantId.Value,
            candidateProfileId: profile.Id,
            candidateEmail: profile.Email,
            candidateName: candidateName,
            sentByAuthSubject: sentBy,
            kind: body.Kind,
            templateSlug: string.IsNullOrWhiteSpace(body.TemplateSlug)
                ? DefaultTemplate(body.Kind)
                : body.TemplateSlug,
            subject: string.IsNullOrWhiteSpace(body.Subject)
                ? DefaultSubject(body.Kind, candidateName)
                : body.Subject);

        db.EsignDocuments.Add(doc);
        await db.SaveChangesAsync(cancellationToken);

        var result = await docuSeal.CreateSubmissionAsync(doc, cancellationToken);
        doc.AttachProviderSubmission(result.ProviderSubmissionId, result.SigningUrl);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(Project(doc));
    }

    private static async Task<Results<Ok<EsignDocumentResponse>, NotFound, ProblemHttpResult>> CancelAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Cancelling an e-sign document requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var doc = await db.EsignDocuments
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            doc.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                title: "Cannot cancel",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(Project(doc));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> WebhookAsync(
        DocuSealWebhookPayload payload,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.SubmissionId))
        {
            return TypedResults.Problem(
                title: "Missing submission id",
                detail: "DocuSeal webhook payload must include the submission id.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Webhook bypasses the tenant filter — DocuSeal doesn't know our
        // tenants. The unique provider id keeps the lookup tenant-safe.
        var doc = await db.EsignDocuments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.ProviderSubmissionId == payload.SubmissionId, cancellationToken);

        if (doc is null)
        {
            return TypedResults.NotFound();
        }

        switch (payload.EventType?.Trim().ToLowerInvariant())
        {
            case "viewed":
                doc.MarkViewed();
                break;
            case "completed":
            case "signed":
                doc.MarkSigned();
                break;
            default:
                return TypedResults.Problem(
                    title: "Unsupported event type",
                    detail: $"Unknown DocuSeal event '{payload.EventType}'.",
                    statusCode: StatusCodes.Status400BadRequest);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static EsignDocumentResponse Project(EsignDocument x) => new(
        x.Id,
        x.CandidateProfileId,
        x.CandidateName,
        x.CandidateEmail,
        x.Kind.ToString(),
        x.TemplateSlug,
        x.Subject,
        x.ProviderSubmissionId,
        x.SigningUrl,
        x.Status.ToString(),
        x.CreatedAtUtc,
        x.SentAtUtc,
        x.ViewedAtUtc,
        x.SignedAtUtc,
        x.CancelledAtUtc);

    private static string DefaultTemplate(EsignDocumentKind kind) => kind switch
    {
        EsignDocumentKind.OfferLetter => "offer_letter_v1",
        EsignDocumentKind.OnboardingPacket => "onboarding_packet_v1",
        _ => "offer_letter_v1",
    };

    private static string DefaultSubject(EsignDocumentKind kind, string candidateName) => kind switch
    {
        EsignDocumentKind.OfferLetter => $"Your offer letter, {candidateName}",
        EsignDocumentKind.OnboardingPacket => $"Welcome aboard — onboarding packet for {candidateName}",
        _ => $"Document to sign — {candidateName}",
    };
}

public sealed record EsignDocumentSendBody(
    Guid CandidateProfileId,
    EsignDocumentKind Kind,
    string? TemplateSlug,
    string? Subject);

public sealed record EsignDocumentListResponse(EsignDocumentResponse[] Items);

public sealed record EsignDocumentResponse(
    Guid Id,
    Guid CandidateProfileId,
    string CandidateName,
    string CandidateEmail,
    string Kind,
    string TemplateSlug,
    string Subject,
    string? ProviderSubmissionId,
    string? SigningUrl,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? ViewedAtUtc,
    DateTimeOffset? SignedAtUtc,
    DateTimeOffset? CancelledAtUtc);

/// <summary>
/// Minimal projection of the DocuSeal webhook payload. Real DocuSeal
/// webhooks carry far more fields; we accept what we need.
/// </summary>
public sealed record DocuSealWebhookPayload(
    string SubmissionId,
    string EventType);
