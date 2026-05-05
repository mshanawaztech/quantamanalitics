using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Storage;

namespace QuantamAnalytics.Api.Endpoints;

public static class CandidateProfileEndpoint
{
    private static readonly HashSet<string> AllowedResumeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private const long MaxResumeBytes = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapCandidateProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/candidate/profile")
            .WithTags("Candidate Profile")
            .RequireAuthorization();

        group.MapGet("/", GetProfileAsync);
        group.MapPut("/", UpdateProfileAsync);
        group.MapPost("/resume", UploadResumeAsync)
            .DisableAntiforgery();

        return app;
    }

    private static async Task<Results<Ok<CandidateProfileResponse>, ProblemHttpResult>> GetProfileAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(user, db, cancellationToken);
        if (profile is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Candidate profiles require a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        return TypedResults.Ok(ToResponse(profile));
    }

    private static async Task<Results<Ok<CandidateProfileResponse>, ProblemHttpResult>> UpdateProfileAsync(
        UpdateCandidateProfileRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(user, db, cancellationToken);
        if (profile is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Candidate profiles require a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        profile.UpdateProfile(
            request.Email,
            request.FullName,
            request.PhoneNumber,
            request.Headline,
            request.Summary);

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(profile));
    }

    private static async Task<Results<Ok<CandidateProfileResponse>, ProblemHttpResult>> UploadResumeAsync(
        IFormFile? file,
        ClaimsPrincipal user,
        AppDbContext db,
        IResumeStorage resumeStorage,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return TypedResults.Problem(
                title: "Resume file required",
                detail: "Select a PDF or Word document to upload.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > MaxResumeBytes)
        {
            return TypedResults.Problem(
                title: "Resume file too large",
                detail: "Resume uploads are limited to 5 MB in the MVP.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!AllowedResumeTypes.Contains(file.ContentType))
        {
            return TypedResults.Problem(
                title: "Unsupported resume format",
                detail: "Upload a PDF, DOC, or DOCX resume.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var profile = await GetOrCreateProfileAsync(user, db, cancellationToken);
        if (profile is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Candidate profiles require a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        if (!resumeStorage.IsConfigured)
        {
            return TypedResults.Problem(
                title: "Resume upload unavailable",
                detail: "Cloudflare R2 storage is not configured in this environment yet.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await using var stream = file.OpenReadStream();
        var upload = await resumeStorage.UploadAsync(
            profile.TenantId,
            profile.AuthSubject,
            file.FileName,
            file.ContentType,
            stream,
            cancellationToken);

        profile.AttachResume(upload.ObjectKey, file.FileName, upload.UploadedAtUtc);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ToResponse(profile));
    }

    private static async Task<CandidateProfile?> GetOrCreateProfileAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var authSubject = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
        var tenantIdClaim = user.FindFirstValue(Roles.TenantIdClaim);

        if (string.IsNullOrWhiteSpace(authSubject) ||
            string.IsNullOrWhiteSpace(email) ||
            !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return null;
        }

        var profile = await db.CandidateProfiles
            .SingleOrDefaultAsync(x => x.AuthSubject == authSubject, cancellationToken);

        if (profile is not null)
        {
            return profile;
        }

        profile = new CandidateProfile(
            tenantId,
            authSubject,
            email,
            user.FindFirstValue("name"));

        db.CandidateProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private static CandidateProfileResponse ToResponse(CandidateProfile profile) =>
        new(
            profile.Id,
            profile.TenantId,
            profile.AuthSubject,
            profile.Email,
            profile.FullName,
            profile.PhoneNumber,
            profile.Headline,
            profile.Summary,
            profile.ResumeFileName,
            profile.ResumeUploadedAtUtc);
}

public sealed record UpdateCandidateProfileRequest(
    string Email,
    string? FullName,
    string? PhoneNumber,
    string? Headline,
    string? Summary);

public sealed record CandidateProfileResponse(
    Guid Id,
    Guid TenantId,
    string AuthSubject,
    string Email,
    string? FullName,
    string? PhoneNumber,
    string? Headline,
    string? Summary,
    string? ResumeFileName,
    DateTimeOffset? ResumeUploadedAtUtc);
