using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Domain.Pdf;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Tenant-scoped branding profile rendered on every invoice PDF — letterhead
/// identity, postal address, bank/ACH remit-to, and default invoice values.
///
/// One row per tenant. GET returns the row (creating defaults on the fly if
/// none exists yet), PUT replaces all editable fields in one shot.
/// </summary>
public static class TenantBrandingEndpoint
{
    public static IEndpointRouteBuilder MapTenantBrandingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenant/branding")
            .WithTags("Tenant · Branding")
            .RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapPut("/", UpdateAsync);
        return app;
    }

    private static async Task<Results<Ok<TenantBrandingResponse>, ProblemHttpResult>> GetAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var branding = await db.TenantBrandings
            .SingleOrDefaultAsync(x => x.TenantId == currentTenant.TenantId, cancellationToken);

        // Auto-create on first read so the SPA always gets a row back —
        // simplifies the UI's "do I POST or PUT?" decision down to "always PUT".
        if (branding is null)
        {
            branding = new TenantBranding(currentTenant.TenantId.Value);
            db.TenantBrandings.Add(branding);
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(Project(branding));
    }

    private static async Task<Results<Ok<TenantBrandingResponse>, ProblemHttpResult>> UpdateAsync(
        UpdateTenantBrandingRequest body,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var branding = await db.TenantBrandings
            .SingleOrDefaultAsync(x => x.TenantId == currentTenant.TenantId, cancellationToken);

        if (branding is null)
        {
            branding = new TenantBranding(currentTenant.TenantId.Value);
            db.TenantBrandings.Add(branding);
        }

        try
        {
            branding.UpdateProfile(
                displayName: body.DisplayName,
                legalName: body.LegalName,
                contactEmail: body.ContactEmail,
                contactPhone: body.ContactPhone,
                addressLine1: body.AddressLine1,
                addressLine2: body.AddressLine2,
                city: body.City,
                stateRegion: body.StateRegion,
                postalCode: body.PostalCode,
                country: body.Country,
                bankName: body.BankName,
                bankAccountNumber: body.BankAccountNumber,
                bankRoutingNumber: body.BankRoutingNumber,
                defaultHourlyRate: body.DefaultHourlyRate,
                defaultCurrency: body.DefaultCurrency,
                defaultPaymentTermsDays: body.DefaultPaymentTermsDays,
                primaryColorHex: body.PrimaryColorHex,
                accentColorHex: body.AccentColorHex);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(title: "Invalid branding request", detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(Project(branding));
    }

    private static TenantBrandingResponse Project(TenantBranding b) => new(
        b.DisplayName ?? BrandingDefaults.DisplayName,
        b.LegalName ?? BrandingDefaults.LegalName,
        b.ContactEmail ?? BrandingDefaults.ContactEmail,
        b.ContactPhone ?? BrandingDefaults.ContactPhone,
        b.AddressLine1,
        b.AddressLine2,
        b.City,
        b.StateRegion,
        b.PostalCode,
        b.Country,
        b.BankName ?? BrandingDefaults.BankName,
        b.BankAccountNumber ?? BrandingDefaults.BankAccountNumber,
        b.BankRoutingNumber ?? BrandingDefaults.BankRoutingNumber,
        b.DefaultHourlyRate ?? BrandingDefaults.DefaultHourlyRate,
        b.DefaultCurrency ?? BrandingDefaults.DefaultCurrency,
        b.DefaultPaymentTermsDays ?? BrandingDefaults.DefaultPaymentTermsDays,
        b.PrimaryColorHex ?? BrandingDefaults.PrimaryColorHex,
        b.AccentColorHex ?? BrandingDefaults.AccentColorHex,
        b.LogoObjectKey is not null,
        b.UpdatedAtUtc);

    private static ProblemHttpResult TenantRequired() => TypedResults.Problem(
        title: "Tenant assignment required",
        detail: "Branding access requires a tenant_id claim in the authenticated session.",
        statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record UpdateTenantBrandingRequest(
    string? DisplayName,
    string? LegalName,
    string? ContactEmail,
    string? ContactPhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateRegion,
    string? PostalCode,
    string? Country,
    string? BankName,
    string? BankAccountNumber,
    string? BankRoutingNumber,
    decimal? DefaultHourlyRate,
    string? DefaultCurrency,
    int? DefaultPaymentTermsDays,
    string? PrimaryColorHex,
    string? AccentColorHex);

public sealed record TenantBrandingResponse(
    string? DisplayName,
    string? LegalName,
    string? ContactEmail,
    string? ContactPhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateRegion,
    string? PostalCode,
    string? Country,
    string? BankName,
    string? BankAccountNumber,
    string? BankRoutingNumber,
    decimal? DefaultHourlyRate,
    string? DefaultCurrency,
    int? DefaultPaymentTermsDays,
    string? PrimaryColorHex,
    string? AccentColorHex,
    bool HasLogo,
    DateTimeOffset UpdatedAtUtc);
