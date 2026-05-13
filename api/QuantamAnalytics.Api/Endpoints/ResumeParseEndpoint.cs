using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Infrastructure.ResumeParsing;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Standalone resume-parsing endpoint. Recruiters drag a resume into the
/// candidate-creation flow and see the parser's best guess at the candidate's
/// name, contact, headline, skills, and work history before deciding whether
/// to commit it as a new <c>CandidateProfile</c>.
/// </summary>
/// <remarks>
/// Stateless — nothing is persisted. The candidate-create endpoint is the
/// one that calls <c>CandidateProfile.UpdateProfile</c> and writes to the
/// DB, and that flow already exists. Splitting parse-vs-persist means a
/// recruiter can tweak the extracted fields client-side before committing,
/// without an interim "draft" row.
/// </remarks>
public static class ResumeParseEndpoint
{
    internal static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
    };

    internal const long MaxBytes = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapResumeParseEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/recruiter/resume/parse", ParseAsync)
            .WithTags("Resume parsing")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess)
            .DisableAntiforgery();

        return app;
    }

    private static async Task<Results<Ok<ParsedResumeResponse>, ProblemHttpResult>> ParseAsync(
        [FromForm(Name = "file")] IFormFile? file,
        IResumeParser parser,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Resume parsing requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        return await ParseUploadedResumeAsync(file, parser, cancellationToken);
    }

    internal static async Task<Results<Ok<ParsedResumeResponse>, ProblemHttpResult>> ParseUploadedResumeAsync(
        IFormFile? file,
        IResumeParser parser,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return TypedResults.Problem(
                title: "Resume file required",
                detail: "Upload a PDF, DOC, DOCX, or TXT file under the 'file' form field.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > MaxBytes)
        {
            return TypedResults.Problem(
                title: "Resume file too large",
                detail: "Resume uploads are limited to 5 MB.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        if (!AllowedTypes.Contains(file.ContentType))
        {
            return TypedResults.Problem(
                title: "Unsupported resume content type",
                detail: $"Got '{file.ContentType}'. Allowed: PDF, DOC, DOCX, TXT.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        await using var stream = file.OpenReadStream();
        var result = await parser.ParseAsync(
            new ResumeParseRequest(file.FileName, file.ContentType, stream),
            cancellationToken);

        return TypedResults.Ok(ToResponse(result));
    }

    internal static ParsedResumeResponse ToResponse(ResumeParseResult result) =>
        new(
            FullName: result.FullName,
            Email: result.Email,
            PhoneNumber: result.PhoneNumber,
            Headline: result.Headline,
            Skills: result.Skills,
            WorkHistory: result.WorkHistory.Select(item => new ParsedResumeWorkItem(
                item.Employer,
                item.Title,
                item.StartDate,
                item.EndDate)).ToArray());
}
