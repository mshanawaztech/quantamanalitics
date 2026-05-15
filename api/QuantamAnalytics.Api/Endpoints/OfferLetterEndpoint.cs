using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Phase 9 / Story 69 — recruiter-facing offer-letter CRUD with explicit
/// state transitions. Tenant scoping rides the global EF query filter;
/// the recruiter role gates the surface. Status transitions follow the
/// OfferLetter aggregate's state machine — endpoints map invalid moves
/// to 409 Conflict.
/// </summary>
public static class OfferLetterEndpoint
{
    public static IEndpointRouteBuilder MapOfferLetterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recruiter/offers")
            .WithTags("Offer letters")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapMethods("/{id:guid}", ["PATCH"], UpdateDraftAsync);
        group.MapPost("/{id:guid}/send", SendAsync);
        group.MapPost("/{id:guid}/accept", AcceptAsync);
        group.MapPost("/{id:guid}/decline", DeclineAsync);
        group.MapPost("/{id:guid}/withdraw", WithdrawAsync);

        return app;
    }

    private static async Task<Results<Ok<OfferListResponse>, ProblemHttpResult>> ListAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var items = await db.OfferLetters
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(Project)
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new OfferListResponse(items));
    }

    private static async Task<Results<Ok<OfferResponse>, NotFound, ProblemHttpResult>> GetAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var offer = await db.OfferLetters.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (offer is null) return TypedResults.NotFound();
        return TypedResults.Ok(Project(offer));
    }

    private static async Task<Results<Created<OfferResponse>, ProblemHttpResult>> CreateAsync(
        CreateOfferRequest body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return TenantOrSubjectRequired();
        }

        OfferLetter offer;
        try
        {
            offer = new OfferLetter(
                currentTenant.TenantId.Value,
                body.CandidateProfileId,
                body.ApplicationId,
                body.Title,
                body.BaseAnnualSalary,
                body.Currency,
                body.StartDate,
                body.BodyMarkdown,
                currentUser.AuthSubject);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return BadRequest(ex.Message);
        }

        db.OfferLetters.Add(offer);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/v1/recruiter/offers/{offer.Id}", Project(offer));
    }

    private static async Task<Results<Ok<OfferResponse>, NotFound, ProblemHttpResult>> UpdateDraftAsync(
        Guid id,
        UpdateOfferDraftRequest body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var offer = await db.OfferLetters.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (offer is null) return TypedResults.NotFound();

        try
        {
            offer.UpdateDraft(body.BodyMarkdown, body.BaseAnnualSalary, body.StartDate);
        }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException) { return BadRequest(ex.Message); }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(Project(offer));
    }

    private static Task<Results<Ok<OfferResponse>, NotFound, ProblemHttpResult>> SendAsync(
        Guid id, AppDbContext db, ICurrentTenant ct, CancellationToken ck) =>
            TransitionAsync(id, db, ct, ck, o => o.Send());

    private static Task<Results<Ok<OfferResponse>, NotFound, ProblemHttpResult>> AcceptAsync(
        Guid id, RecordOfferResponseRequest body, AppDbContext db, ICurrentTenant ct, CancellationToken ck) =>
            TransitionAsync(id, db, ct, ck, o => o.RecordAcceptance(body?.Note));

    private static Task<Results<Ok<OfferResponse>, NotFound, ProblemHttpResult>> DeclineAsync(
        Guid id, RecordOfferResponseRequest body, AppDbContext db, ICurrentTenant ct, CancellationToken ck) =>
            TransitionAsync(id, db, ct, ck, o => o.RecordDecline(body?.Note));

    private static Task<Results<Ok<OfferResponse>, NotFound, ProblemHttpResult>> WithdrawAsync(
        Guid id, AppDbContext db, ICurrentTenant ct, CancellationToken ck) =>
            TransitionAsync(id, db, ct, ck, o => o.Withdraw());

    private static async Task<Results<Ok<OfferResponse>, NotFound, ProblemHttpResult>> TransitionAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken,
        Action<OfferLetter> action)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var offer = await db.OfferLetters.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (offer is null) return TypedResults.NotFound();

        try { action(offer); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(Project(offer));
    }

    private static OfferResponse Project(OfferLetter x) => new(
        x.Id,
        x.CandidateProfileId,
        x.ApplicationId,
        x.Title,
        x.BaseAnnualSalary,
        x.Currency,
        x.StartDate,
        x.BodyMarkdown,
        x.Status.ToString(),
        x.CreatedAtUtc,
        x.UpdatedAtUtc,
        x.SentAtUtc,
        x.ResolvedAtUtc,
        x.CandidateResponseNote);

    private static ProblemHttpResult TenantRequired() => TypedResults.Problem(
        title: "Tenant assignment required",
        statusCode: StatusCodes.Status412PreconditionFailed);

    private static ProblemHttpResult TenantOrSubjectRequired() => TypedResults.Problem(
        title: "Authenticated tenant subject required",
        detail: "Offer creation needs a tenant_id claim and an authenticated subject.",
        statusCode: StatusCodes.Status412PreconditionFailed);

    private static ProblemHttpResult BadRequest(string detail) => TypedResults.Problem(
        title: "Invalid offer request",
        detail: detail,
        statusCode: StatusCodes.Status400BadRequest);

    private static ProblemHttpResult Conflict(string detail) => TypedResults.Problem(
        title: "Offer state transition rejected",
        detail: detail,
        statusCode: StatusCodes.Status409Conflict);
}

public sealed record CreateOfferRequest(
    Guid CandidateProfileId,
    Guid? ApplicationId,
    string Title,
    decimal BaseAnnualSalary,
    string Currency,
    DateOnly StartDate,
    string BodyMarkdown);

public sealed record UpdateOfferDraftRequest(
    string BodyMarkdown,
    decimal BaseAnnualSalary,
    DateOnly StartDate);

public sealed record RecordOfferResponseRequest(string? Note);

public sealed record OfferResponse(
    Guid Id,
    Guid CandidateProfileId,
    Guid? ApplicationId,
    string Title,
    decimal BaseAnnualSalary,
    string Currency,
    DateOnly StartDate,
    string BodyMarkdown,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    string? CandidateResponseNote);

public sealed record OfferListResponse(OfferResponse[] Items);
