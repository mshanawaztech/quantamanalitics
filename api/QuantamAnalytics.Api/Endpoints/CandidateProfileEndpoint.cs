using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.ResumeParsing;
using QuantamAnalytics.Infrastructure.Storage;
using QuantamAnalytics.Infrastructure.Tenancy;

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
        group.MapGet("/applications", GetApplicationsAsync);
        group.MapGet("/timeline", GetTimelineAsync);
        group.MapGet("/applications/{applicationId:guid}/timeline", GetApplicationTimelineAsync);
        group.MapPut("/", UpdateProfileAsync);
        group.MapPost("/resume/parse", ParseResumeAsync)
            .DisableAntiforgery();
        group.MapPost("/resume", UploadResumeAsync)
            .DisableAntiforgery();

        return app;
    }

    private static async Task<Results<Ok<CandidateProfileResponse>, ProblemHttpResult>> GetProfileAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(user, db, currentTenant, cancellationToken);
        if (profile is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Candidate profiles require a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        return TypedResults.Ok(ToResponse(profile));
    }

    private static async Task<Results<Ok<CandidateApplicationsResponse>, ProblemHttpResult>> GetApplicationsAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(user, db, currentTenant, cancellationToken);
        if (profile is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Candidate profiles require a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var applications = await db.Applications
            .Where(x => x.CandidateProfileId == profile.Id)
            .OrderByDescending(x => x.AppliedAtUtc)
            .Join(
                db.Jobs,
                application => application.JobId,
                job => job.Id,
                (application, job) => new CandidateApplicationResponse(
                    application.Id,
                    job.Id,
                    job.Title,
                    job.Slug,
                    job.Location,
                    application.Status.ToString(),
                    application.Note,
                    application.AppliedAtUtc,
                    application.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new CandidateApplicationsResponse(applications));
    }

    private static async Task<Results<Ok<CandidateTimelineFeedResponse>, ProblemHttpResult>> GetTimelineAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(user, db, currentTenant, cancellationToken);
        if (profile is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Candidate profiles require a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var applicationMap = await db.Applications
            .Where(x => x.CandidateProfileId == profile.Id)
            .Join(
                db.Jobs,
                application => application.JobId,
                job => job.Id,
                (application, job) => new
                {
                    application.Id,
                    application.Status,
                    JobId = job.Id,
                    JobTitle = job.Title,
                    JobSlug = job.Slug,
                })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (applicationMap.Count == 0)
        {
            return TypedResults.Ok(new CandidateTimelineFeedResponse([]));
        }

        var items = await db.ApplicationTimelineEvents
            .Where(x =>
                x.CandidateProfileId == profile.Id &&
                x.Audience == ApplicationTimelineAudience.CandidateAndRecruiter)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        var response = items
            .Where(x => applicationMap.ContainsKey(x.ApplicationId))
            .Select(x =>
            {
                var application = applicationMap[x.ApplicationId];
                return new CandidateTimelineFeedItemResponse(
                    x.Id,
                    x.ApplicationId,
                    application.JobId,
                    application.JobTitle,
                    application.JobSlug,
                    x.EventType.ToString(),
                    x.Title,
                    x.Description,
                    x.ActorLabel,
                    application.Status.ToString(),
                    x.OccurredAtUtc);
            })
            .ToArray();

        return TypedResults.Ok(new CandidateTimelineFeedResponse(response));
    }

    private static async Task<Results<Ok<ApplicationTimelineResponse>, NotFound, ProblemHttpResult>> GetApplicationTimelineAsync(
        Guid applicationId,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(user, db, currentTenant, cancellationToken);
        if (profile is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Candidate profiles require a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var application = await db.Applications
            .Where(x => x.Id == applicationId && x.CandidateProfileId == profile.Id)
            .Join(
                db.Jobs,
                app => app.JobId,
                job => job.Id,
                (app, job) => new
                {
                    Application = app,
                    JobTitle = job.Title,
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (application is null)
        {
            return TypedResults.NotFound();
        }

        var events = await db.ApplicationTimelineEvents
            .Where(x =>
                x.ApplicationId == applicationId &&
                x.Audience == ApplicationTimelineAudience.CandidateAndRecruiter)
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => new TimelineEventResponse(
                x.Id,
                x.EventType.ToString(),
                x.Audience.ToString(),
                x.Title,
                x.Description,
                x.ActorLabel,
                x.OccurredAtUtc))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new ApplicationTimelineResponse(
            application.Application.Id,
            application.Application.CandidateProfileId,
            application.Application.CandidateName,
            application.Application.CandidateEmail,
            application.JobTitle,
            application.Application.Status.ToString(),
            events));
    }

    private static async Task<Results<Ok<CandidateProfileResponse>, ProblemHttpResult>> UpdateProfileAsync(
        UpdateCandidateProfileRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(user, db, currentTenant, cancellationToken);
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

    private static async Task<Results<Ok<ParsedResumeResponse>, ProblemHttpResult>> ParseResumeAsync(
        IFormFile? file,
        ClaimsPrincipal user,
        IResumeParser parser,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var authSubject = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");

        // Use ICurrentTenant — same middleware-fallback rationale as
        // GetOrCreateProfileAsync. JWT claim-direct reads bypass the
        // tenant_memberships table and break bootstrap-via-banner users.
        if (string.IsNullOrWhiteSpace(authSubject) ||
            string.IsNullOrWhiteSpace(email) ||
            currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Candidate profiles require a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        return await ResumeParseEndpoint.ParseUploadedResumeAsync(file, parser, cancellationToken);
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

        var profile = await GetOrCreateProfileAsync(user, db, currentTenant, cancellationToken);
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
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var authSubject = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
        var displayName = user.FindFirstValue("name");

        // Use ICurrentTenant instead of reading the JWT claim directly.
        // TenantResolutionMiddleware falls back to the tenant_memberships
        // table when the JWT has no tenant_id claim, so users bootstrapped
        // via /me/tenant/join-demo end up tenant-scoped here too.
        if (string.IsNullOrWhiteSpace(authSubject) ||
            string.IsNullOrWhiteSpace(email) ||
            currentTenant.TenantId is null)
        {
            return null;
        }

        var tenantId = currentTenant.TenantId.Value;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var profile = await db.CandidateProfiles
            .SingleOrDefaultAsync(x => x.AuthSubject == authSubject, cancellationToken);

        if (profile is not null)
        {
            return profile;
        }

        profile = await db.CandidateProfiles
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Email == normalizedEmail,
                cancellationToken);

        if (profile is not null)
        {
            profile.AttachAuthenticatedIdentity(authSubject, normalizedEmail, displayName);
            await db.SaveChangesAsync(cancellationToken);
            return profile;
        }

        profile = new CandidateProfile(
            tenantId,
            authSubject,
            normalizedEmail,
            displayName);

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

public sealed record CandidateApplicationResponse(
    Guid Id,
    Guid JobId,
    string JobTitle,
    string JobSlug,
    string Location,
    string Status,
    string? Note,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CandidateApplicationsResponse(CandidateApplicationResponse[] Items);
