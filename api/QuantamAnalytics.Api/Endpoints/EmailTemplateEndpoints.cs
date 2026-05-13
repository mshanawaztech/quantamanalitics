using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Recruiter-only CRUD over <see cref="EmailTemplate"/>. Tenant scoping is
/// enforced by the global query filter on <see cref="AppDbContext"/>; the
/// endpoint never accepts a <c>tenant_id</c> from the client.
/// </summary>
public static class EmailTemplateEndpoints
{
    // Postgres SQLSTATE for "unique_violation" — used to map the unique
    // (tenant_id, slug) violation to a 409 Conflict instead of a 500.
    private const string PostgresUniqueViolationSqlState = "23505";
    private static readonly string[] SupportedMergeFields =
    [
        "candidate_name",
        "candidate_email",
        "company",
        "job_title",
        "interview_date",
        "recruiter_name",
        "portal_link",
        "offer_amount",
        "onboarding_due_date",
    ];

    private static readonly EmailTemplatePresetResponse[] Presets =
    [
        new(
            "interview-invite",
            "Interview Invite",
            "Schedule the first recruiter screen with candidate-facing date context.",
            "Interview invite for {{job_title}}",
            """
            Hi {{candidate_name}},

            Thanks again for your interest in {{job_title}} at {{company}}.

            We'd like to invite you to a recruiter screen on {{interview_date}}. You can reply here with any scheduling constraints, or use {{portal_link}} to review the role details again before we meet.

            Best,
            {{recruiter_name}}
            """,
            SupportedMergeFields),
        new(
            "rejection-note",
            "Rejection Note",
            "Candidate-safe decline template with room for clear but kind closure.",
            "Update on your {{job_title}} application",
            """
            Hi {{candidate_name}},

            Thanks for taking the time to speak with {{company}} about {{job_title}}.

            We are moving forward with other candidates for this opening, but we appreciated the chance to learn more about your background. We'll keep your profile on file for future roles that look like a closer match.

            Thank you,
            {{recruiter_name}}
            """,
            SupportedMergeFields),
        new(
            "onboarding-kickoff",
            "Onboarding Kickoff",
            "Share next steps and portal follow-up items once a candidate accepts.",
            "Welcome to {{company}}",
            """
            Hi {{candidate_name}},

            Welcome aboard. Your onboarding checklist for {{job_title}} is now live in {{portal_link}}.

            Please complete the initial items by {{onboarding_due_date}} so the team can keep your start on track.

            Thanks,
            {{recruiter_name}}
            """,
            SupportedMergeFields),
        new(
            "offer-ready",
            "Offer Ready",
            "Compensation-ready message with a simple offer summary.",
            "{{company}} offer for {{job_title}}",
            """
            Hi {{candidate_name}},

            We're excited to share an offer for {{job_title}} at {{company}}.

            Base compensation: {{offer_amount}}

            Please review the attached offer package and let us know if you would like to walk through any details together.

            Best,
            {{recruiter_name}}
            """,
            SupportedMergeFields),
    ];

    public static IEndpointRouteBuilder MapEmailTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recruiter/email-templates")
            .WithTags("Recruiter Email Templates")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        group.MapGet("/catalog", GetCatalogAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/preview", PreviewAsync);
        group.MapMethods("/{id:guid}", ["PATCH"], UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    private static Ok<EmailTemplateCatalogResponse> GetCatalogAsync() =>
        TypedResults.Ok(new EmailTemplateCatalogResponse(
            Presets,
            SupportedMergeFields));

    private static async Task<Results<Ok<EmailTemplateListResponse>, ProblemHttpResult>> ListAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var rows = await db.EmailTemplates
            .OrderBy(x => x.Name)
            .Select(x => Project(x))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new EmailTemplateListResponse(rows));
    }

    private static async Task<Results<Ok<EmailTemplateResponse>, NotFound, ProblemHttpResult>> GetAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var template = await db.EmailTemplates.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null) return TypedResults.NotFound();

        return TypedResults.Ok(Project(template));
    }

    private static async Task<Results<Created<EmailTemplateResponse>, Conflict<string>, ProblemHttpResult>> CreateAsync(
        CreateEmailTemplateRequest body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        if (string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return AuthSubjectRequired();
        }

        EmailTemplate template;
        try
        {
            template = new EmailTemplate(
                tenantId: currentTenant.TenantId.Value,
                slug: body.Slug,
                name: body.Name,
                subject: body.Subject,
                bodyMarkdown: body.BodyMarkdown,
                createdByAuthSubject: currentUser.AuthSubject);
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }

        db.EmailTemplates.Add(template);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The composite (tenant_id, slug) unique index trips here —
            // surface a clean 409 so the recruiter UI can prompt for a
            // different slug instead of treating it as a server error.
            return TypedResults.Conflict(
                $"A template with slug '{body.Slug}' already exists for this tenant.");
        }

        return TypedResults.Created(
            $"/api/v1/recruiter/email-templates/{template.Id}",
            Project(template));
    }

    private static Results<Ok<EmailTemplatePreviewResponse>, ProblemHttpResult> PreviewAsync(
        PreviewEmailTemplateRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Subject) || string.IsNullOrWhiteSpace(body.BodyMarkdown))
        {
            return BadRequestProblem("Preview requires both a subject and body.");
        }

        var mergeFields = body.MergeFields
            ?.Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(
                x => x.Key.Trim(),
                x => x.Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var unsupportedKeys = mergeFields.Keys
            .Where(x => !SupportedMergeFields.Contains(x, StringComparer.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unsupportedKeys.Length > 0)
        {
            return BadRequestProblem(
                $"Unsupported merge field(s): {string.Join(", ", unsupportedKeys)}. " +
                $"Allowed: {string.Join(", ", SupportedMergeFields)}.");
        }

        return TypedResults.Ok(new EmailTemplatePreviewResponse(
            Render(body.Subject, mergeFields),
            Render(body.BodyMarkdown, mergeFields),
            mergeFields));
    }

    private static async Task<Results<Ok<EmailTemplateResponse>, NotFound, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdateEmailTemplateRequest body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var template = await db.EmailTemplates.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null) return TypedResults.NotFound();

        // PATCH semantics — only the fields the caller sent are touched.
        // Fall back to the existing values for anything they omitted so the
        // entity validator (which forbids blanks) keeps the row consistent.
        var name = body.Name ?? template.Name;
        var subject = body.Subject ?? template.Subject;
        var bodyMarkdown = body.BodyMarkdown ?? template.BodyMarkdown;

        try
        {
            template.UpdateContent(name, subject, bodyMarkdown);
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(Project(template));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> DeleteAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var template = await db.EmailTemplates.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null) return TypedResults.NotFound();

        db.EmailTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static EmailTemplateResponse Project(EmailTemplate x) => new(
        x.Id,
        x.Slug,
        x.Name,
        x.Subject,
        x.BodyMarkdown,
        x.CreatedByAuthSubject,
        x.CreatedAtUtc,
        x.UpdatedAtUtc);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg
        && pg.SqlState == PostgresUniqueViolationSqlState;

    private static ProblemHttpResult TenantRequired() => TypedResults.Problem(
        title: "Tenant assignment required",
        detail: "Email-template access requires a tenant_id claim in the authenticated session.",
        statusCode: StatusCodes.Status412PreconditionFailed);

    private static ProblemHttpResult AuthSubjectRequired() => TypedResults.Problem(
        title: "Authenticated subject missing",
        detail: "Cannot record the template author without a subject claim on the authenticated session.",
        statusCode: StatusCodes.Status401Unauthorized);

    private static ProblemHttpResult BadRequestProblem(string message) =>
        TypedResults.Problem(
            title: "Invalid email-template request",
            detail: message,
            statusCode: StatusCodes.Status400BadRequest);

    private static string Render(
        string template,
        IReadOnlyDictionary<string, string> mergeFields)
    {
        var rendered = template;
        foreach (var field in mergeFields)
        {
            rendered = rendered.Replace(
                $"{{{{{field.Key}}}}}",
                field.Value,
                StringComparison.OrdinalIgnoreCase);
        }

        return rendered;
    }
}

public sealed record CreateEmailTemplateRequest(
    string Slug,
    string Name,
    string Subject,
    string BodyMarkdown);

public sealed record UpdateEmailTemplateRequest(
    string? Name,
    string? Subject,
    string? BodyMarkdown);

public sealed record EmailTemplateListResponse(EmailTemplateResponse[] Items);

public sealed record EmailTemplateCatalogResponse(
    EmailTemplatePresetResponse[] Presets,
    string[] SupportedMergeFields);

public sealed record EmailTemplatePresetResponse(
    string Slug,
    string Name,
    string Description,
    string Subject,
    string BodyMarkdown,
    string[] MergeFields);

public sealed record PreviewEmailTemplateRequest(
    string Subject,
    string BodyMarkdown,
    Dictionary<string, string?>? MergeFields);

public sealed record EmailTemplatePreviewResponse(
    string Subject,
    string BodyMarkdown,
    IReadOnlyDictionary<string, string> MergeFields);

public sealed record EmailTemplateResponse(
    Guid Id,
    string Slug,
    string Name,
    string Subject,
    string BodyMarkdown,
    string CreatedByAuthSubject,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
