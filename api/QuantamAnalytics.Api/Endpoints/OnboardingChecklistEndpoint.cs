using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

public static class OnboardingChecklistEndpoint
{
    public static IEndpointRouteBuilder MapOnboardingChecklistEndpoints(this IEndpointRouteBuilder app)
    {
        // Recruiter / PlatformAdmin write surface.
        var recruiterGroup = app.MapGroup("/api/v1/onboarding")
            .WithTags("Onboarding Checklist")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        recruiterGroup.MapGet("/candidates/{candidateProfileId:guid}", ListForCandidateAsync);
        recruiterGroup.MapPost("/candidates/{candidateProfileId:guid}/items", AssignItemsAsync);
        recruiterGroup.MapPost("/candidates/{candidateProfileId:guid}/apply-template", ApplyTemplateAsync);
        recruiterGroup.MapPost("/items/{id:guid}/approve", ApproveAsync);
        recruiterGroup.MapPost("/items/{id:guid}/reject", RejectAsync);

        // Candidate self-service surface.
        var candidateGroup = app.MapGroup("/api/v1/onboarding/me")
            .WithTags("Onboarding Checklist")
            .RequireAuthorization(AuthorizationPolicies.RequireCandidate);

        candidateGroup.MapGet("/items", ListMineAsync);
        candidateGroup.MapPost("/apply-template", ApplyTemplateMineAsync);
        candidateGroup.MapPost("/items/{id:guid}/submit", SubmitMineAsync);

        return app;
    }

    // ── Recruiter-facing handlers ──────────────────────────────────────────

    private static async Task<Results<Ok<OnboardingChecklistResponse>, ProblemHttpResult>> ListForCandidateAsync(
        Guid candidateProfileId,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var items = await db.OnboardingChecklistItems
            .Where(x => x.CandidateProfileId == candidateProfileId)
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(BuildResponse(items));
    }

    private static async Task<Results<Ok<OnboardingChecklistResponse>, NotFound, ProblemHttpResult>> AssignItemsAsync(
        Guid candidateProfileId,
        AssignOnboardingItemsBody body,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        if (body.Items is null || body.Items.Count == 0)
        {
            return TypedResults.Problem(
                title: "No items provided",
                detail: "Provide at least one onboarding item to assign.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var assignedBy = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(assignedBy))
        {
            return TypedResults.Problem(
                title: "Authenticated subject missing",
                detail: "Cannot record the assigning recruiter without a subject claim.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var profile = await db.CandidateProfiles
            .FirstOrDefaultAsync(x => x.Id == candidateProfileId, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        foreach (var input in body.Items)
        {
            var item = new OnboardingChecklistItem(
                tenantId: currentTenant.TenantId.Value,
                candidateProfileId: profile.Id,
                assignedByAuthSubject: assignedBy,
                itemType: input.ItemType,
                title: input.Title,
                instructions: input.Instructions);

            db.OnboardingChecklistItems.Add(item);
        }

        await db.SaveChangesAsync(cancellationToken);

        var items = await db.OnboardingChecklistItems
            .Where(x => x.CandidateProfileId == profile.Id)
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(BuildResponse(items));
    }

    /// <summary>
    /// Applies the default onboarding template for a worker type to a
    /// candidate in one call — the real-world phased checklist. Idempotent:
    /// only adds steps the candidate doesn't already have, so re-applying or
    /// switching types never duplicates rows.
    /// </summary>
    private static async Task<Results<Ok<OnboardingChecklistResponse>, NotFound, ProblemHttpResult>> ApplyTemplateAsync(
        Guid candidateProfileId,
        ApplyOnboardingTemplateBody body,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var assignedBy = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(assignedBy))
        {
            return TypedResults.Problem(
                title: "Authenticated subject missing",
                detail: "Cannot record the assigning recruiter without a subject claim.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var profile = await db.CandidateProfiles
            .FirstOrDefaultAsync(x => x.Id == candidateProfileId, cancellationToken);
        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        var items = await ApplyTemplateCoreAsync(
            db, currentTenant.TenantId.Value, profile.Id, body.ConsultantType, assignedBy, cancellationToken);
        return TypedResults.Ok(BuildResponse(items));
    }

    /// <summary>
    /// Adds every template step the candidate is missing and returns the full
    /// current item set. Shared by the recruiter and candidate-self entry points.
    /// </summary>
    private static async Task<OnboardingChecklistItem[]> ApplyTemplateCoreAsync(
        AppDbContext db,
        Guid tenantId,
        Guid candidateProfileId,
        ConsultantType consultantType,
        string assignedBy,
        CancellationToken cancellationToken)
    {
        var existing = (await db.OnboardingChecklistItems
            .Where(x => x.CandidateProfileId == candidateProfileId)
            .Select(x => x.ItemType)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var step in OnboardingTemplateCatalog.For(consultantType))
        {
            if (existing.Contains(step.ItemType))
            {
                continue;
            }

            db.OnboardingChecklistItems.Add(new OnboardingChecklistItem(
                tenantId: tenantId,
                candidateProfileId: candidateProfileId,
                assignedByAuthSubject: assignedBy,
                itemType: step.ItemType,
                title: step.Title,
                instructions: step.Instructions));
        }

        await db.SaveChangesAsync(cancellationToken);

        return await db.OnboardingChecklistItems
            .Where(x => x.CandidateProfileId == candidateProfileId)
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<Results<Ok<OnboardingItemResponse>, NotFound, ProblemHttpResult>> ApproveAsync(
        Guid id,
        OnboardingDecisionBody body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken) =>
        await ApplyReviewerActionAsync(id, body, db, currentTenant,
            (item, note) => item.Approve(note),
            cancellationToken);

    private static async Task<Results<Ok<OnboardingItemResponse>, NotFound, ProblemHttpResult>> RejectAsync(
        Guid id,
        OnboardingDecisionBody body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Note))
        {
            return TypedResults.Problem(
                title: "Reject note required",
                detail: "A reviewer note is required when rejecting an onboarding item.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await ApplyReviewerActionAsync(id, body, db, currentTenant,
            (item, note) => item.Reject(note!),
            cancellationToken);
    }

    private static async Task<Results<Ok<OnboardingItemResponse>, NotFound, ProblemHttpResult>> ApplyReviewerActionAsync(
        Guid id,
        OnboardingDecisionBody body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        Action<OnboardingChecklistItem, string?> mutate,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var item = await db.OnboardingChecklistItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            mutate(item, body.Note);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                title: "Invalid transition",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(Project(item));
    }

    // ── Candidate-facing handlers ──────────────────────────────────────────

    private static async Task<Results<Ok<OnboardingChecklistResponse>, ProblemHttpResult>> ListMineAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return TypedResults.Problem(
                title: "Authenticated subject missing",
                detail: "Cannot resolve the candidate without a subject claim.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var profile = await db.CandidateProfiles
            .FirstOrDefaultAsync(x => x.AuthSubject == subject, cancellationToken);

        if (profile is null)
        {
            return TypedResults.Ok(new OnboardingChecklistResponse([]));
        }

        var items = await db.OnboardingChecklistItems
            .Where(x => x.CandidateProfileId == profile.Id)
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(BuildResponse(items));
    }

    /// <summary>
    /// Self-service start: a consultant applies their own default onboarding
    /// template, creating a candidate profile from their session if they don't
    /// have one yet. Lets a new consultant immediately see a real, phased
    /// checklist instead of an empty page.
    /// </summary>
    private static async Task<Results<Ok<OnboardingChecklistResponse>, ProblemHttpResult>> ApplyTemplateMineAsync(
        ApplyOnboardingTemplateBody body,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return TypedResults.Problem(
                title: "Authenticated subject missing",
                detail: "Cannot resolve the consultant without a subject claim.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var profile = await db.CandidateProfiles
            .FirstOrDefaultAsync(x => x.AuthSubject == subject, cancellationToken);
        if (profile is null)
        {
            var email = user.FindFirstValue(ClaimTypes.Email)
                ?? user.FindFirstValue("email")
                ?? subject + "@local";
            var name = user.FindFirstValue("name") ?? email;
            profile = new CandidateProfile(currentTenant.TenantId.Value, subject, email, name);
            db.CandidateProfiles.Add(profile);
            await db.SaveChangesAsync(cancellationToken);
        }

        var items = await ApplyTemplateCoreAsync(
            db, currentTenant.TenantId.Value, profile.Id, body.ConsultantType, subject, cancellationToken);
        return TypedResults.Ok(BuildResponse(items));
    }

    private static async Task<Results<Ok<OnboardingItemResponse>, NotFound, ProblemHttpResult>> SubmitMineAsync(
        Guid id,
        OnboardingSubmitBody body,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return TypedResults.Problem(
                title: "Authenticated subject missing",
                detail: "Cannot resolve the candidate without a subject claim.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var profile = await db.CandidateProfiles
            .FirstOrDefaultAsync(x => x.AuthSubject == subject, cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        var item = await db.OnboardingChecklistItems
            .FirstOrDefaultAsync(
                x => x.Id == id && x.CandidateProfileId == profile.Id,
                cancellationToken);

        if (item is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            item.Submit(body.Note);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                title: "Invalid transition",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(Project(item));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Projects materialized items into the response, phase-grouped and
    /// ordered. Callers must materialize entities first — the phase metadata
    /// is computed in C# (not EF-translatable).
    /// </summary>
    private static OnboardingChecklistResponse BuildResponse(IEnumerable<OnboardingChecklistItem> items) =>
        new(items.Select(Project).OrderBy(x => x.SortOrder).ToArray());

    private static OnboardingItemResponse Project(OnboardingChecklistItem x) => new(
        x.Id,
        x.CandidateProfileId,
        x.ItemType.ToString(),
        x.Title,
        x.Instructions,
        x.Status.ToString(),
        x.CandidateNote,
        x.ReviewerNote,
        x.AssignedAtUtc,
        x.SubmittedAtUtc,
        x.ReviewedAtUtc,
        x.UpdatedAtUtc,
        OnboardingPhases.PhaseFor(x.ItemType).ToString(),
        OnboardingPhases.IsRequired(x.ItemType),
        OnboardingPhases.SortOrder(x.ItemType));

    private static ProblemHttpResult TenantRequiredProblem() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            detail: "Onboarding checklist requires a tenant_id claim in the authenticated session.",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record AssignOnboardingItemsBody(IReadOnlyList<AssignOnboardingItemInput> Items);

public sealed record AssignOnboardingItemInput(
    OnboardingItemType ItemType,
    string Title,
    string? Instructions);

public sealed record OnboardingDecisionBody(string? Note);

public sealed record OnboardingSubmitBody(string? Note);

public sealed record OnboardingChecklistResponse(OnboardingItemResponse[] Items);

public sealed record OnboardingItemResponse(
    Guid Id,
    Guid CandidateProfileId,
    string ItemType,
    string Title,
    string? Instructions,
    string Status,
    string? CandidateNote,
    string? ReviewerNote,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Phase,
    bool IsRequired,
    int SortOrder);

public sealed record ApplyOnboardingTemplateBody(ConsultantType ConsultantType);
