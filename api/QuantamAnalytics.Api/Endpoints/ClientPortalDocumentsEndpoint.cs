using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Read-only client visibility into the offer letters and onboarding
/// packets that recruiters have routed through DocuSeal. Pairs with the
/// PR-34 client-portal jobs surface — same role, same tenant scoping,
/// just a different read shape.
/// </summary>
/// <remarks>
/// What the client gets back:
/// - Document kind (offer letter vs onboarding packet)
/// - Status (Drafted / Sent / Viewed / Signed / Cancelled)
/// - Lifecycle timestamps (sent / viewed / signed / cancelled)
/// - Candidate name + email — the client already knows these (the
///   submission that produced the offer was sent to them by name).
/// - The DocuSeal SigningUrl, if any. The client typically doesn't sign
///   the document themselves (that's the candidate), but the URL is the
///   canonical reference and they may want to follow up. Treat it as
///   informational — never as something the client should click on
///   behalf of the candidate.
///
/// What the client does NOT get:
/// - <c>SentByAuthSubject</c> (which recruiter pushed the document) —
///   internal to the staffing firm.
/// - <c>ProviderSubmissionId</c> (DocuSeal-internal) — leaks provider
///   coupling that future Phase 5 swap-out should not expose.
/// - <c>TemplateSlug</c> — internal recruiter taxonomy.
///
/// Tenant scoping uses the global query filter exclusively — same
/// shape as ClientPortalJobsEndpoint, same tenant-isolation IT pattern
/// catches regressions.
/// </remarks>
public static class ClientPortalDocumentsEndpoint
{
    public static IEndpointRouteBuilder MapClientPortalDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/client/documents")
            .WithTags("Client Portal · Documents")
            .RequireAuthorization(AuthorizationPolicies.RequireClientPortalAccess);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);

        return app;
    }

    private static async Task<Results<Ok<ClientPortalDocumentsResponse>, ProblemHttpResult>> ListAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var docs = await db.EsignDocuments
            .OrderByDescending(x => x.SignedAtUtc ?? x.SentAtUtc ?? x.UpdatedAtUtc)
            .Select(x => Project(x))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new ClientPortalDocumentsResponse(docs));
    }

    private static async Task<Results<Ok<ClientPortalDocumentResponse>, NotFound, ProblemHttpResult>> GetAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var doc = await db.EsignDocuments
            .Where(x => x.Id == id)
            .Select(x => Project(x))
            .SingleOrDefaultAsync(cancellationToken);

        return doc is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(doc);
    }

    private static ClientPortalDocumentResponse Project(EsignDocument x) => new(
        Id: x.Id,
        CandidateProfileId: x.CandidateProfileId,
        CandidateName: x.CandidateName,
        CandidateEmail: x.CandidateEmail,
        Kind: x.Kind.ToString(),
        Subject: x.Subject,
        Status: x.Status.ToString(),
        SigningUrl: x.SigningUrl,
        SentAtUtc: x.SentAtUtc,
        ViewedAtUtc: x.ViewedAtUtc,
        SignedAtUtc: x.SignedAtUtc,
        CancelledAtUtc: x.CancelledAtUtc,
        UpdatedAtUtc: x.UpdatedAtUtc);

    private static ProblemHttpResult TenantRequiredProblem() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            detail: "The client documents portal requires a tenant_id claim in the authenticated session.",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record ClientPortalDocumentsResponse(ClientPortalDocumentResponse[] Items);

public sealed record ClientPortalDocumentResponse(
    Guid Id,
    Guid CandidateProfileId,
    string CandidateName,
    string CandidateEmail,
    string Kind,
    string Subject,
    string Status,
    string? SigningUrl,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? ViewedAtUtc,
    DateTimeOffset? SignedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset UpdatedAtUtc);
