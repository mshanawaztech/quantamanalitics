using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Domain.Pdf;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Direct-billing invoices submitted by contractors. v2 ships line items,
/// auto-minted invoice numbers, separate issue/due dates, a tax line, and
/// a client name. The state machine is unchanged from v1.
///
/// Audiences:
/// - Contractor (any authenticated tenant member): list / create / update /
///   submit their own invoices. Auth-subject scoping inside each handler.
/// - Recruiter / PlatformAdmin: read across the tenant.
/// - Client / PlatformAdmin: approve or reject submitted invoices.
/// - PlatformAdmin only: mark approved invoices as Paid.
/// </summary>
public static class InvoiceEndpoint
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var contractor = app.MapGroup("/api/v1/contractor/invoices")
            .WithTags("Invoices · Contractor")
            .RequireAuthorization();
        contractor.MapGet("/", ListMineAsync);
        contractor.MapPost("/", CreateAsync);
        contractor.MapPut("/{id:guid}", UpdateMineAsync);
        contractor.MapPost("/{id:guid}/submit", SubmitMineAsync);
        contractor.MapGet("/{id:guid}/pdf", DownloadPdfAsync);

        var recruiter = app.MapGroup("/api/v1/recruiter/invoices")
            .WithTags("Invoices · Recruiter")
            .RequireAuthorization(AuthorizationPolicies.RequirePayrollAccess);
        recruiter.MapGet("/", ListAcrossTenantAsync);
        recruiter.MapPost("/{id:guid}/mark-paid", MarkPaidAsync);

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

        var invoices = await db.Invoices
            .Where(x => x.ContractorAuthSubject == subject)
            .Include(x => x.LineItems)
            .OrderByDescending(x => x.IssueDateUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var rows = invoices.Select(Project).ToArray();
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

        var inputs = ProjectLineItemInputs(body.LineItems);
        if (inputs.Count == 0)
        {
            return BadRequestProblem("An invoice must have at least one line item.");
        }

        // Mint the human-readable invoice number inside the same transaction
        // that inserts the invoice. The row lock on invoice_number_sequences
        // serializes concurrent inserts so two contractors on the same tenant
        // can't be issued the same number.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var year = body.IssueDateUtc.Year;
        var sequence = await db.InvoiceNumberSequences
            .SingleOrDefaultAsync(
                x => x.TenantId == currentTenant.TenantId.Value && x.Year == year,
                cancellationToken);

        if (sequence is null)
        {
            sequence = new InvoiceNumberSequence(currentTenant.TenantId.Value, year);
            db.InvoiceNumberSequences.Add(sequence);
        }

        var invoiceNumber = sequence.MintNext();

        try
        {
            var invoice = new Invoice(
                tenantId: currentTenant.TenantId.Value,
                contractorAuthSubject: subject,
                contractorEmail: email,
                invoiceNumber: invoiceNumber,
                clientName: body.ClientName ?? string.Empty,
                issueDateUtc: body.IssueDateUtc,
                dueDateUtc: body.DueDateUtc,
                periodStartUtc: body.PeriodStartUtc,
                periodEndUtc: body.PeriodEndUtc,
                currency: body.Currency,
                taxRate: body.TaxRate,
                lineItems: inputs,
                notes: body.Notes);

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return TypedResults.Created(
                $"/api/v1/contractor/invoices/{invoice.Id}",
                Project(invoice));
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return BadRequestProblem(ex.Message);
        }
    }

    private static async Task<Results<Ok<InvoiceResponse>, NotFound, ProblemHttpResult>> UpdateMineAsync(
        Guid id,
        UpdateInvoiceRequest body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = currentUser.AuthSubject;
        if (string.IsNullOrWhiteSpace(subject)) return SubjectRequired();

        var invoice = await db.Invoices
            .Include(x => x.LineItems)
            .SingleOrDefaultAsync(
                x => x.Id == id && x.ContractorAuthSubject == subject,
                cancellationToken);

        if (invoice is null) return TypedResults.NotFound();

        var inputs = ProjectLineItemInputs(body.LineItems);
        if (inputs.Count == 0)
        {
            return BadRequestProblem("An invoice must have at least one line item.");
        }

        try
        {
            invoice.UpdateDraft(
                clientName: body.ClientName ?? string.Empty,
                issueDateUtc: body.IssueDateUtc,
                dueDateUtc: body.DueDateUtc,
                periodStartUtc: body.PeriodStartUtc,
                periodEndUtc: body.PeriodEndUtc,
                currency: body.Currency,
                taxRate: body.TaxRate,
                lineItems: inputs,
                notes: body.Notes);

            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(Project(invoice));
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return InvalidTransitionProblem(ex.Message);
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
            .Include(x => x.LineItems)
            .SingleOrDefaultAsync(
                x => x.Id == id && x.ContractorAuthSubject == subject,
                cancellationToken);

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

    private static async Task<Results<FileContentHttpResult, NotFound, ProblemHttpResult>> DownloadPdfAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IInvoicePdfRenderer pdfRenderer,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = currentUser.AuthSubject;
        if (string.IsNullOrWhiteSpace(subject)) return SubjectRequired();

        var invoice = await db.Invoices
            .Include(x => x.LineItems)
            .SingleOrDefaultAsync(
                x => x.Id == id && x.ContractorAuthSubject == subject,
                cancellationToken);

        if (invoice is null) return TypedResults.NotFound();

        var branding = await db.TenantBrandings
            .SingleOrDefaultAsync(x => x.TenantId == currentTenant.TenantId, cancellationToken);

        // Logo fetch deferred: v3.2 will pull bytes from R2 if LogoObjectKey
        // is set. For v3.1 the renderer falls back to the text identity.
        var pdf = pdfRenderer.Render(invoice, branding, logoBytes: null);

        return TypedResults.File(
            fileContents: pdf,
            contentType: "application/pdf",
            fileDownloadName: $"Invoice-{invoice.InvoiceNumber}.pdf");
    }

    // ── Recruiter / admin handlers ─────────────────────────────────────

    private static async Task<Results<Ok<InvoiceListResponse>, ProblemHttpResult>> ListAcrossTenantAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var invoices = await db.Invoices
            .Include(x => x.LineItems)
            .OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var rows = invoices.Select(Project).ToArray();
        return TypedResults.Ok(new InvoiceListResponse(rows));
    }

    private static async Task<Results<Ok<InvoiceResponse>, NotFound, ProblemHttpResult>> MarkPaidAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var invoice = await db.Invoices
            .Include(x => x.LineItems)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
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

        var invoices = await db.Invoices
            .Where(x => x.Status == InvoiceStatus.Submitted ||
                        x.Status == InvoiceStatus.Approved ||
                        x.Status == InvoiceStatus.Rejected ||
                        x.Status == InvoiceStatus.Paid)
            .Include(x => x.LineItems)
            .OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var rows = invoices.Select(Project).ToArray();
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

        var invoice = await db.Invoices
            .Include(x => x.LineItems)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
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

        var invoice = await db.Invoices
            .Include(x => x.LineItems)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
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

    /// <summary>
    /// Maps DTO line items to the domain value object, filtering out fully
    /// blank rows (a contractor pressing "+Add item" then leaving it empty).
    /// </summary>
    private static List<InvoiceLineItemInput> ProjectLineItemInputs(
        IEnumerable<InvoiceLineItemRequest>? lineItems)
    {
        if (lineItems is null) return [];

        return lineItems
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Description) ||
                x.Hours > 0 ||
                x.Rate > 0)
            .Select(x => new InvoiceLineItemInput(
                Description: string.IsNullOrWhiteSpace(x.Description) ? "—" : x.Description,
                Hours: x.Hours,
                Rate: x.Rate))
            .ToList();
    }

    private static InvoiceResponse Project(Invoice x) => new(
        x.Id,
        x.InvoiceNumber,
        x.ContractorEmail,
        x.ClientName,
        x.IssueDateUtc,
        x.DueDateUtc,
        x.PeriodStartUtc,
        x.PeriodEndUtc,
        x.Hours,
        x.Subtotal,
        x.TaxRate,
        x.TaxAmount,
        x.Amount,
        x.Currency,
        x.Notes,
        x.Status.ToString(),
        x.SubmittedAtUtc,
        x.ReviewedAtUtc,
        x.ReviewerNote,
        x.PaidAtUtc,
        x.UpdatedAtUtc,
        x.LineItems
            .OrderBy(li => li.SortOrder)
            .Select(li => new InvoiceLineItemResponse(
                li.Id,
                li.Description,
                li.Hours,
                li.Rate,
                li.Amount,
                li.SortOrder))
            .ToArray());

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
    string? ClientName,
    DateOnly IssueDateUtc,
    DateOnly DueDateUtc,
    DateOnly PeriodStartUtc,
    DateOnly PeriodEndUtc,
    string Currency,
    decimal TaxRate,
    InvoiceLineItemRequest[] LineItems,
    string? Notes);

public sealed record UpdateInvoiceRequest(
    string? ClientName,
    DateOnly IssueDateUtc,
    DateOnly DueDateUtc,
    DateOnly PeriodStartUtc,
    DateOnly PeriodEndUtc,
    string Currency,
    decimal TaxRate,
    InvoiceLineItemRequest[] LineItems,
    string? Notes);

public sealed record InvoiceLineItemRequest(
    string Description,
    decimal Hours,
    decimal Rate);

public sealed record InvoiceDecisionRequest(string? Note);

public sealed record InvoiceListResponse(InvoiceResponse[] Items);

public sealed record InvoiceResponse(
    Guid Id,
    string InvoiceNumber,
    string ContractorEmail,
    string ClientName,
    DateOnly IssueDateUtc,
    DateOnly DueDateUtc,
    DateOnly PeriodStartUtc,
    DateOnly PeriodEndUtc,
    decimal Hours,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal Amount,
    string Currency,
    string? Notes,
    string Status,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewerNote,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset UpdatedAtUtc,
    InvoiceLineItemResponse[] LineItems);

public sealed record InvoiceLineItemResponse(
    Guid Id,
    string Description,
    decimal Hours,
    decimal Rate,
    decimal Amount,
    int SortOrder);
