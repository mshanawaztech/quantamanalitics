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

    public static IEndpointRouteBuilder MapEmailTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recruiter/email-templates")
            .WithTags("Recruiter Email Templates")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapMethods("/{id:guid}", ["PATCH"], UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

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

public sealed record EmailTemplateResponse(
    Guid Id,
    string Slug,
    string Name,
    string Subject,
    string BodyMarkdown,
    string CreatedByAuthSubject,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
