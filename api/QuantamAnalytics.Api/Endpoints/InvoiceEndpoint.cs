using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Direct-billing invoices submitted by contractors. Mirrors the
/// timesheet review flow but for the case where a contractor invoices
/// the staffing firm directly rather than going through payroll.
///
/// Three audiences:
/// - Contractor (any authenticated tenant member): create / submit /
///   list their own invoices. Auth-subject filter inside the endpoint
///   keeps one contractor from seeing another's invoices on the same
///   tenant.
/// - Recruiter / PlatformAdmin: read across the tenant.
/// - Client / PlatformAdmin: approve or reject submitted invoices.
/// - PlatformAdmin only: mark approved invoices as Paid (this hooks
///   the existing QuickBooks / Stripe baseline by changing status —
///   no money moves here, only state).
/// </summary>
public static class InvoiceEndpoint
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        // Contractor surface — auth-subject scoping happens inside each handler.
        var contractor = app.MapGroup("/api/v1/contractor/invoices")
            .WithTags("Invoices · Contractor")
            .RequireAuthorization();
        contractor.MapGet("/", ListMineAsync);
        contractor.MapPost("/", CreateAsync);
        contractor.MapPost("/{id:guid}/submit", SubmitMineAsync);

        // Recruiter / PlatformAdmin surface — read across the tenant.
        var recruiter = app.MapGroup("/api/v1/recruiter/invoices")
            .WithTags("Invoices · Recruiter")
            .RequireAuthorization(AuthorizationPolicies.RequirePayrollAccess);
        recruiter.MapGet("/", ListAcrossTenantAsync);
        recruiter.MapPost("/{id:guid}/mark-paid", MarkPaidAsync);

        // Client / PlatformAdmin surface — approve or reject.
        var client = app.MapGroup("/api/v1/client/invoices")
            .WithTags("Invoices · Client")
            .RequireAuthorization(AuthorizationPolicies.RequireTimeApprovalAccess);
        client.MapGet("/", ListSubmittedAsync);
        client.MapPost("/{id:guid}/approve", ApproveAsync);
        client.MapPost("/{id:guid}/reject", RejectAsync);

        return app;
    }

    // ── Contractor handlers ────────────────────────────────────────────

    private static async Task<Results<Ok<InvoiceListResponse>, ProblemHttpResult>> ListMineAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = currentUser.AuthSubject;
        if (string.IsNullOrWhiteSpace(subject)) return SubjectRequired();

        var rows = await db.Invoices
            .Where(x => x.ContractorAuthSubject == subject)
            .OrderByDescending(x => x.PeriodStartUtc)
            .Select(x => Project(x))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new InvoiceListResponse(rows));
    }

    private static async Task<Results<Created<InvoiceResponse>, ProblemHttpResult>> CreateAsync(
        CreateInvoiceRequest body,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = currentUser.AuthSubject;
        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirst("email")?.Value;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            return SubjectRequired();
        }

        try
        {
            var invoice = new Invoice(
                tenantId: currentTenant.TenantId.Value,
                contractorAuthSubject: subject,
                contractorEmail: email,
                periodStartUtc: body.PeriodStartUtc,
                periodEndUtc: body.PeriodEndUtc,
                hours: body.Hours,
                amount: body.Amount,
                currency: body.Currency,
                notes: body.Notes);

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(cancellationToken);

            return TypedResults.Created(
                $"/api/v1/contractor/invoices/{invoice.Id}",
                Project(invoice));
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }
    }

    private static async Task<Results<Ok<InvoiceResponse>, NotFound, ProblemHttpResult>> SubmitMineAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = currentUser.AuthSubject;
        if (string.IsNullOrWhiteSpace(subject)) return SubjectRequired();

        var invoice = await db.Invoices
            .SingleOrDefaultAsync(x => x.Id == id && x.ContractorAuthSubject == subject, cancellationToken);

        if (invoice is null) return TypedResults.NotFound();

        try
        {
            invoice.Submit();
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(Project(invoice));
        }
        catch (InvalidOperationException ex)
        {
            return InvalidTransitionProblem(ex.Message);
        }
    }

    // ── Recruiter / admin handlers ─────────────────────────────────────

    private static async Task<Results<Ok<InvoiceListResponse>, ProblemHttpResult>> ListAcrossTenantAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var rows = await db.Invoices
            .OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .Select(x => Project(x))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new InvoiceListResponse(rows));
    }

    private static async Task<Results<Ok<InvoiceResponse>, NotFound, ProblemHttpResult>> MarkPaidAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var invoice = await db.Invoices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null) return TypedResults.NotFound();

        try
        {
            invoice.MarkPaid();
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(Project(invoice));
        }
        catch (InvalidOperationException ex)
        {
            return InvalidTransitionProblem(ex.Message);
        }
    }

    // ── Client handlers ────────────────────────────────────────────────

    private static async Task<Results<Ok<InvoiceListResponse>, ProblemHttpResult>> ListSubmittedAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var rows = await db.Invoices
            .Where(x => x.Status == InvoiceStatus.Submitted ||
                        x.Status == InvoiceStatus.Approved ||
                        x.Status == InvoiceStatus.Rejected ||
                        x.Status == InvoiceStatus.Paid)
            .OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .Select(x => Project(x))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new InvoiceListResponse(rows));
    }

    private static async Task<Results<Ok<InvoiceResponse>, NotFound, ProblemHttpResult>> ApproveAsync(
        Guid id,
        InvoiceDecisionRequest body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = currentUser.AuthSubject;
        if (string.IsNullOrWhiteSpace(subject)) return SubjectRequired();

        var invoice = await db.Invoices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null) return TypedResults.NotFound();

        try
        {
            invoice.Approve(subject, body.Note);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(Project(invoice));
        }
        catch (InvalidOperationException ex)
        {
            return InvalidTransitionProblem(ex.Message);
        }
    }

    private static async Task<Results<Ok<InvoiceResponse>, NotFound, ProblemHttpResult>> RejectAsync(
        Guid id,
        InvoiceDecisionRequest body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = currentUser.AuthSubject;
        if (string.IsNullOrWhiteSpace(subject)) return SubjectRequired();
        if (string.IsNullOrWhiteSpace(body.Note))
        {
            return BadRequestProblem("Reject note is required.");
        }

        var invoice = await db.Invoices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null) return TypedResults.NotFound();

        try
        {
            invoice.Reject(subject, body.Note);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(Project(invoice));
        }
        catch (InvalidOperationException ex)
        {
            return InvalidTransitionProblem(ex.Message);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static InvoiceResponse Project(Invoice x) => new(
        x.Id,
        x.ContractorEmail,
        x.PeriodStartUtc,
        x.PeriodEndUtc,
        x.Hours,
        x.Amount,
        x.Currency,
        x.Notes,
        x.Status.ToString(),
        x.SubmittedAtUtc,
        x.ReviewedAtUtc,
        x.ReviewerNote,
        x.PaidAtUtc,
        x.UpdatedAtUtc);

    private static ProblemHttpResult TenantRequired() => TypedResults.Problem(
        title: "Tenant assignment required",
        detail: "Invoice access requires a tenant_id claim in the authenticated session.",
        statusCode: StatusCodes.Status412PreconditionFailed);

    private static ProblemHttpResult SubjectRequired() => TypedResults.Problem(
        title: "Authenticated subject missing",
        detail: "Cannot resolve the contractor without a subject claim on the authenticated session.",
        statusCode: StatusCodes.Status401Unauthorized);

    private static ProblemHttpResult InvalidTransitionProblem(string message) =>
        TypedResults.Problem(title: "Invalid invoice state transition", detail: message,
            statusCode: StatusCodes.Status409Conflict);

    private static ProblemHttpResult BadRequestProblem(string message) =>
        TypedResults.Problem(title: "Invalid invoice request", detail: message,
            statusCode: StatusCodes.Status400BadRequest);
}

public sealed record CreateInvoiceRequest(
    DateOnly PeriodStartUtc,
    DateOnly PeriodEndUtc,
    decimal Hours,
    decimal Amount,
    string Currency,
    string? Notes);

public sealed record InvoiceDecisionRequest(string? Note);

public sealed record InvoiceListResponse(InvoiceResponse[] Items);

public sealed record InvoiceResponse(
    Guid Id,
    string ContractorEmail,
    DateOnly PeriodStartUtc,
    DateOnly PeriodEndUtc,
    decimal Hours,
    decimal Amount,
    string Currency,
    string? Notes,
    string Status,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewerNote,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset UpdatedAtUtc);
