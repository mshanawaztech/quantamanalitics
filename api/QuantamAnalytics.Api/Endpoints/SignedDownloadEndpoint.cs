using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Storage;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Tenant-safe signed-URL minting for private R2 objects.
///
/// Two surfaces:
///   - <c>POST /api/v1/me/resume/download-link</c> — a candidate fetches a
///     short-lived URL for their own resume. Owner is matched on
///     <c>ICurrentUser.AuthSubject</c>; the global tenant filter scopes the
///     profile lookup.
///   - <c>POST /api/v1/recruiter/candidates/{id}/resume/download-link</c> —
///     a recruiter fetches the link for a tenant candidate's resume.
///     Recipient is gated on <see cref="AuthorizationPolicies.RequireRecruitingAccess"/>;
///     tenant scoping again handled by the global query filter.
///
/// The endpoint never returns the raw object key. The signed URL is
/// short-lived (<see cref="DefaultTtl"/>) and carries an explicit
/// <c>Content-Disposition</c> override so the browser surfaces the
/// candidate-friendly filename instead of the random storage key.
/// </summary>
public static class SignedDownloadEndpoint
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapSignedDownloadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/me/resume/download-link", MintOwnResumeLinkAsync)
            .WithTags("File security")
            .RequireAuthorization();

        app.MapPost("/api/v1/recruiter/candidates/{candidateId:guid}/resume/download-link", MintRecruiterResumeLinkAsync)
            .WithTags("File security")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        return app;
    }

    private static async Task<Results<Ok<SignedDownloadResponse>, NotFound, ProblemHttpResult>> MintOwnResumeLinkAsync(
        AppDbContext db,
        IResumeStorage storage,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return SubjectRequired();
        }

        var subject = currentUser.AuthSubject;

        // Candidate is matched on auth subject — cross-tenant access is
        // already impossible because the global query filter clamps the
        // profile lookup to the current tenant.
        var profile = await db.CandidateProfiles
            .Where(c => c.AuthSubject == subject)
            .Select(c => new
            {
                c.ResumeObjectKey,
                c.ResumeFileName,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null || profile.ResumeObjectKey is null)
        {
            return TypedResults.NotFound();
        }

        return await MintAsync(storage, profile.ResumeObjectKey, profile.ResumeFileName, cancellationToken);
    }

    private static async Task<Results<Ok<SignedDownloadResponse>, NotFound, ProblemHttpResult>> MintRecruiterResumeLinkAsync(
        Guid candidateId,
        AppDbContext db,
        IResumeStorage storage,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return SubjectRequired();
        }

        var profile = await db.CandidateProfiles
            .Where(c => c.Id == candidateId)
            .Select(c => new
            {
                c.ResumeObjectKey,
                c.ResumeFileName,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null || profile.ResumeObjectKey is null)
        {
            return TypedResults.NotFound();
        }

        return await MintAsync(storage, profile.ResumeObjectKey, profile.ResumeFileName, cancellationToken);
    }

    private static async Task<Results<Ok<SignedDownloadResponse>, NotFound, ProblemHttpResult>> MintAsync(
        IResumeStorage storage,
        string objectKey,
        string? downloadFileName,
        CancellationToken cancellationToken)
    {
        if (!storage.IsConfigured)
        {
            return TypedResults.Problem(
                title: "File storage disabled",
                detail: "This environment has no configured object store.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var signed = await storage.CreateSignedDownloadUrlAsync(
            objectKey, DefaultTtl, downloadFileName, cancellationToken);

        if (signed is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new SignedDownloadResponse(signed.Url, signed.ExpiresAtUtc));
    }

    private static ProblemHttpResult SubjectRequired() =>
        TypedResults.Problem(
            title: "Authenticated tenant subject required",
            detail: "Signed-link minting requires a tenant_id claim and an authenticated subject.",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record SignedDownloadResponse(string Url, DateTimeOffset ExpiresAtUtc);
