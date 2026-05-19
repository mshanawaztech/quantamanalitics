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
        contractor.MapGet("/{id:guid}/csv", DownloadCsvAsync);

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
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = ResolveSubject(currentUser, user, currentTenant);

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

        // Try every reasonable place an Auth0 / OIDC JWT might carry the
        // user's email. Sub claim probing happens in the middleware; here
        // we shore up the create-side identity so the contractor always
        // has SOMETHING attributable to them on the invoice.
        var email = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirst("email")?.Value
            ?? user.FindFirst("https://schemas.quantamanalytics.com/email")?.Value
            ?? user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue("upn");

        // Subject precedence: middleware-resolved → email-derived → tenant-scoped fallback.
        // The last fallback unblocks demos where Auth0 is misconfigured and
        // the JWT is missing every standard identity claim. It's tenant-scoped
        // so two such users on the same tenant still can't impersonate each other.
        var subject = currentUser.AuthSubject;
        if (string.IsNullOrWhiteSpace(subject))
        {
            subject = !string.IsNullOrWhiteSpace(email)
                ? "email|" + email.Trim().ToLowerInvariant()
                : "tenant|" + currentTenant.TenantId.Value.ToString("N");
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            email = "unknown@" + currentTenant.TenantId.Value.ToString("N") + ".local";
        }

        var inputs = ProjectLineItemInputs(body.LineItems);
        if (inputs.Count == 0)
        {
            return BadRequestProblem("An invoice must have at least one line item.");
        }

        // Resolve the invoice number: user-provided takes precedence (after
        // a uniqueness check), otherwise we mint the next sequential number.
        // Both paths run inside the same transaction as the insert so two
        // concurrent submits can't collide on the same human-readable code.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        string invoiceNumber;
        var customNumber = body.InvoiceNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(customNumber))
        {
            // Custom-numbered: enforce per-tenant uniqueness ourselves so the
            // contractor gets a friendly 409, not a Postgres unique-index 23505.
            var exists = await db.Invoices
                .AnyAsync(
                    x => x.TenantId == currentTenant.TenantId.Value &&
                         x.InvoiceNumber == customNumber,
                    cancellationToken);
            if (exists)
            {
                await tx.RollbackAsync(cancellationToken);
                return BadRequestProblem(
                    $"Invoice number '{customNumber}' is already in use for this tenant.");
            }
            invoiceNumber = customNumber;
        }
        else
        {
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
            invoiceNumber = sequence.MintNext();
        }

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
                notes: body.Notes,
                remitBankName: body.RemitBankName,
                remitAccountNumber: body.RemitAccountNumber,
                remitRoutingNumber: body.RemitRoutingNumber,
                remitContactPhone: body.RemitContactPhone,
                vendorName: body.VendorName);

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
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = ResolveSubject(currentUser, user, currentTenant);

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
                notes: body.Notes,
                remitBankName: body.RemitBankName,
                remitAccountNumber: body.RemitAccountNumber,
                remitRoutingNumber: body.RemitRoutingNumber,
                remitContactPhone: body.RemitContactPhone,
                vendorName: body.VendorName);

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
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = ResolveSubject(currentUser, user, currentTenant);

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
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IInvoicePdfRenderer pdfRenderer,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = ResolveSubject(currentUser, user, currentTenant);

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

    private static async Task<Results<FileContentHttpResult, NotFound, ProblemHttpResult>> DownloadCsvAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();
        var subject = ResolveSubject(currentUser, user, currentTenant);

        var invoice = await db.Invoices
            .Include(x => x.LineItems)
            .SingleOrDefaultAsync(
                x => x.Id == id && x.ContractorAuthSubject == subject,
                cancellationToken);

        if (invoice is null) return TypedResults.NotFound();

        // Plain-CSV (RFC 4180) — header + one row per line item. Fields with
        // commas/quotes/newlines are double-quote-wrapped with internal quotes
        // doubled. Good enough for QuickBooks / Excel imports.
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("invoice_number,issue_date,due_date,period_start,period_end,client,line_no,description,week_start,week_end,days,hours_per_day,hours,rate,amount,currency,notes");
        var n = 1;
        foreach (var li in invoice.LineItems.OrderBy(li => li.SortOrder))
        {
            sb.Append(EscapeCsv(invoice.InvoiceNumber)).Append(',');
            sb.Append(invoice.IssueDateUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(invoice.DueDateUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(invoice.PeriodStartUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(invoice.PeriodEndUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(invoice.ClientName)).Append(',');
            sb.Append(n++).Append(',');
            sb.Append(EscapeCsv(li.Description)).Append(',');
            sb.Append(li.WeekStartUtc?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "").Append(',');
            sb.Append(li.WeekEndUtc?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "").Append(',');
            sb.Append(li.DaysWorked.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(li.HoursPerDay.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(li.Hours.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(li.Rate.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(li.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(invoice.Currency).Append(',');
            sb.Append(EscapeCsv(li.Notes ?? ""));
            sb.AppendLine();
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        return TypedResults.File(
            fileContents: bytes,
            contentType: "text/csv; charset=utf-8",
            fileDownloadName: $"Invoice-{invoice.InvoiceNumber}.csv");
    }

    /// <summary>RFC 4180 CSV field escaping.</summary>
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var needsQuotes = value.IndexOfAny(['"', ',', '\n', '\r']) >= 0;
        var escaped = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
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

    /// <summary>
    /// Last-resort subject resolver — never returns null/empty. Used by the
    /// contractor-side read/write endpoints so demos and misconfigured Auth0
    /// tenants don't dead-end on a "Cannot resolve subject" 401. The order
    /// matches the middleware: NameIdentifier → sub URI → raw "sub" → email
    /// derived → tenant-scoped fallback.
    /// </summary>
    private static string ResolveSubject(
        ICurrentUser currentUser,
        ClaimsPrincipal user,
        ICurrentTenant currentTenant)
    {
        if (!string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return currentUser.AuthSubject;
        }

        var email = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email")
            ?? user.FindFirstValue("preferred_username");
        if (!string.IsNullOrWhiteSpace(email))
        {
            return "email|" + email.Trim().ToLowerInvariant();
        }

        return "tenant|" + currentTenant.TenantId!.Value.ToString("N");
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
                x.DaysWorked > 0 ||
                x.HoursPerDay > 0 ||
                x.Rate > 0)
            .Select(x => new InvoiceLineItemInput(
                Description: string.IsNullOrWhiteSpace(x.Description) ? "—" : x.Description,
                WeekStartUtc: x.WeekStartUtc,
                DaysWorked: x.DaysWorked,
                HoursPerDay: x.HoursPerDay,
                Rate: x.Rate,
                Notes: x.Notes))
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
                li.WeekStartUtc,
                li.WeekEndUtc,
                li.DaysWorked,
                li.HoursPerDay,
                li.Hours,
                li.Rate,
                li.Amount,
                li.Notes,
                li.SortOrder))
            .ToArray(),
        x.RemitBankName,
        x.RemitAccountNumber,
        x.RemitRoutingNumber,
        x.RemitContactPhone,
        x.VendorName);

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
    string? InvoiceNumber,
    string? ClientName,
    DateOnly IssueDateUtc,
    DateOnly DueDateUtc,
    DateOnly PeriodStartUtc,
    DateOnly PeriodEndUtc,
    string Currency,
    decimal TaxRate,
    InvoiceLineItemRequest[] LineItems,
    string? Notes,
    // qa005 — per-invoice Remit-to override. Send NULL to inherit
    // the tenant's branding default at PDF render time.
    string? RemitBankName = null,
    string? RemitAccountNumber = null,
    string? RemitRoutingNumber = null,
    string? RemitContactPhone = null,
    string? VendorName = null);

public sealed record UpdateInvoiceRequest(
    string? ClientName,
    DateOnly IssueDateUtc,
    DateOnly DueDateUtc,
    DateOnly PeriodStartUtc,
    DateOnly PeriodEndUtc,
    string Currency,
    decimal TaxRate,
    InvoiceLineItemRequest[] LineItems,
    string? Notes,
    string? RemitBankName = null,
    string? RemitAccountNumber = null,
    string? RemitRoutingNumber = null,
    string? RemitContactPhone = null,
    string? VendorName = null);

/// <summary>
/// v4 line-item request. Description + week-of (Monday) + days × hours/day
/// + rate + optional notes. Server computes <c>Hours = Days × HoursPerDay</c>
/// and <c>Amount = Hours × Rate</c>.
/// </summary>
public sealed record InvoiceLineItemRequest(
    string Description,
    DateOnly? WeekStartUtc,
    decimal DaysWorked,
    decimal HoursPerDay,
    decimal Rate,
    string? Notes);

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
    InvoiceLineItemResponse[] LineItems,
    // qa005 — null means "no per-invoice override; PDF will use
    // the tenant's branding default." The client can show these
    // as placeholder text in form fields to make the inheritance
    // visible to the user.
    string? RemitBankName = null,
    string? RemitAccountNumber = null,
    string? RemitRoutingNumber = null,
    string? RemitContactPhone = null,
    string? VendorName = null);

public sealed record InvoiceLineItemResponse(
    Guid Id,
    string Description,
    DateOnly? WeekStartUtc,
    DateOnly? WeekEndUtc,
    decimal DaysWorked,
    decimal HoursPerDay,
    decimal Hours,
    decimal Rate,
    decimal Amount,
    string? Notes,
    int SortOrder);
