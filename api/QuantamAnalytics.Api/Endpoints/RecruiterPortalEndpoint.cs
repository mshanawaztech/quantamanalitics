using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

public static class RecruiterPortalEndpoint
{
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromDays(7);

    public static IEndpointRouteBuilder MapRecruiterPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recruiter")
            .WithTags("Recruiter Portal")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        group.MapGet("/jobs", GetJobsAsync);
        group.MapPost("/jobs", CreateJobAsync);
        group.MapPut("/jobs/{jobId:guid}", UpdateJobAsync);

        group.MapGet("/applications", GetApplicationsAsync);
        group.MapPost("/applications/bulk-status", BulkUpdateApplicationStatusAsync);
        group.MapPost("/applications/bulk-tags", BulkUpdateApplicationTagsAsync);
        group.MapPost("/applications/filters", SaveApplicationFilterPresetAsync);
        group.MapDelete("/applications/filters/{filterPresetId:guid}", DeleteApplicationFilterPresetAsync);
        group.MapGet("/applications/{applicationId:guid}/timeline", GetApplicationTimelineAsync);
        group.MapPost("/applications/{applicationId:guid}/timeline/comments", AddApplicationTimelineCommentAsync);
        group.MapGet("/candidates/activity", GetCandidateActivityAsync);
        group.MapGet("/invoice-ready", GetInvoiceReadyAsync);
        group.MapGet("/invoice-handoff", GetInvoiceHandoffAsync);
        group.MapGet("/invoice-handoff/quickbooks.csv", DownloadQuickBooksCsvAsync);
        group.MapPost("/applications/{applicationId:guid}/status", UpdateApplicationStatusAsync);

        return app;
    }

    private static async Task<Results<Ok<RecruiterJobResponse[]>, ProblemHttpResult>> GetJobsAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var jobs = await db.Jobs
            .OrderByDescending(x => x.PostedOnUtc)
            .Select(x => new RecruiterJobResponse(
                x.Id,
                x.Title,
                x.Slug,
                x.Location,
                x.Summary,
                x.Description,
                x.PostedOnUtc,
                x.IsPublished))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(jobs);
    }

    private static async Task<Results<Ok<RecruiterJobResponse>, ProblemHttpResult>> CreateJobAsync(
        UpsertRecruiterJobRequest request,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var slug = Slugify(request.Title);
        if (await db.Jobs.AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            slug = $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 9, 160)];
        }

        var job = new Job(
            currentTenant.TenantId.Value,
            request.Title,
            slug,
            request.Location,
            request.Summary,
            request.Description,
            request.PostedOnUtc ?? DateOnly.FromDateTime(DateTime.UtcNow.Date));

        if (!request.IsPublished)
        {
            job.Unpublish();
        }

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ToJobResponse(job));
    }

    private static async Task<Results<Ok<RecruiterJobResponse>, NotFound, ProblemHttpResult>> UpdateJobAsync(
        Guid jobId,
        UpsertRecruiterJobRequest request,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var job = await db.Jobs.SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        job.UpdateDetails(
            request.Title,
            request.Location,
            request.Summary,
            request.Description,
            request.PostedOnUtc ?? job.PostedOnUtc);

        if (request.IsPublished)
        {
            job.Publish();
        }
        else
        {
            job.Unpublish();
        }

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ToJobResponse(job));
    }

    private static async Task<Results<Ok<RecruiterApplicationsBoardResponse>, ProblemHttpResult>> GetApplicationsAsync(
        [AsParameters] RecruiterApplicationQuery request,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var search = NormalizeSearchValue(request.Search);
        var status = NormalizeSearchValue(request.Status);
        var tag = NormalizeSearchValue(request.Tag);
        var location = NormalizeSearchValue(request.Location);
        var stuckBeforeUtc = nowUtc - StuckThreshold;

        if (!string.IsNullOrWhiteSpace(status) && !IsSupportedFilterStatus(status))
        {
            return TypedResults.Problem(
                title: "Unsupported filter status",
                detail: "Use Applied, Interviewing, OfferSent, Hired, or Rejected.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var query = db.Applications
            .AsNoTracking()
            .Join(
                db.Jobs.AsNoTracking(),
                application => application.JobId,
                job => job.Id,
                (application, job) => new { Application = application, Job = job })
            .Join(
                db.CandidateProfiles.AsNoTracking(),
                joined => joined.Application.CandidateProfileId,
                candidate => candidate.Id,
                (joined, candidate) => new
                {
                    joined.Application,
                    joined.Job,
                    Candidate = candidate,
                });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Application.CandidateName, pattern) ||
                EF.Functions.ILike(x.Application.CandidateEmail, pattern) ||
                EF.Functions.ILike(x.Job.Title, pattern) ||
                EF.Functions.ILike(x.Job.Location, pattern) ||
                (x.Candidate.Headline != null && EF.Functions.ILike(x.Candidate.Headline, pattern)) ||
                (x.Candidate.Summary != null && EF.Functions.ILike(x.Candidate.Summary, pattern)) ||
                db.ApplicationTags.Any(t =>
                    t.ApplicationId == x.Application.Id &&
                    EF.Functions.ILike(t.Name, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Application.Status.ToString() == status);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(x =>
                db.ApplicationTags.Any(t =>
                    t.ApplicationId == x.Application.Id &&
                    t.Name == tag));
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var pattern = $"%{location}%";
            query = query.Where(x => EF.Functions.ILike(x.Job.Location, pattern));
        }

        if (request.StuckOnly == true)
        {
            query = query.Where(x => x.Application.UpdatedAtUtc <= stuckBeforeUtc);
        }

        var rows = await query
            .OrderByDescending(x => x.Application.AppliedAtUtc)
            .ToArrayAsync(cancellationToken);

        var applicationIds = rows
            .Select(x => x.Application.Id)
            .ToArray();

        var tagsByApplicationId = await GetTagsByApplicationIdAsync(
            db,
            applicationIds,
            cancellationToken);

        var availableTags = await db.ApplicationTags
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var availableLocations = await db.Jobs
            .AsNoTracking()
            .OrderBy(x => x.Location)
            .Select(x => x.Location)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var currentSubject = ResolveActorSubject(user);
        var savedFilters =
            currentSubject is null
                ? []
                : await db.RecruiterApplicationFilterPresets
                    .AsNoTracking()
                    .Where(x => x.CreatedByAuthSubject == currentSubject)
                    .OrderBy(x => x.Name)
                    .Select(x => new RecruiterApplicationFilterPresetResponse(
                        x.Id,
                        x.Name,
                        x.Search,
                        x.Status,
                        x.Tag,
                        x.Location,
                        x.StuckOnly,
                        x.CreatedAtUtc,
                        x.UpdatedAtUtc))
                    .ToArrayAsync(cancellationToken);

        var items = rows
            .Select(x => ToRecruiterApplicationResponse(
                x.Application,
                x.Job.Id,
                x.Job.Title,
                nowUtc,
                tagsByApplicationId.GetValueOrDefault(x.Application.Id, [])))
            .ToArray();

        return TypedResults.Ok(new RecruiterApplicationsBoardResponse(
            items,
            availableTags,
            availableLocations,
            savedFilters));
    }

    private static async Task<Results<Ok<RecruiterBulkStatusMoveResponse>, ProblemHttpResult>> BulkUpdateApplicationStatusAsync(
        BulkUpdateApplicationStatusRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var applicationIds = request.ApplicationIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (applicationIds.Length == 0)
        {
            return TypedResults.Problem(
                title: "Applications required",
                detail: "Select at least one candidate card before running a bulk move.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!IsSupportedStatus(request.Status))
        {
            return TypedResults.Problem(
                title: "Unsupported application status",
                detail: "Use Interviewing, OfferSent, Hired, or Rejected.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var applications = await db.Applications
            .Where(x => applicationIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var movedCount = 0;
        var actorLabel = ResolveActorLabel(user);
        foreach (var application in applications)
        {
            if (application.Status.ToString() == request.Status)
            {
                continue;
            }

            ApplyStatusTransition(application, request.Status);
            movedCount++;
            db.ApplicationTimelineEvents.Add(CreateStageTimelineEvent(
                currentTenant.TenantId.Value,
                application,
                actorLabel));
        }

        await db.SaveChangesAsync(cancellationToken);

        var nowUtc = DateTimeOffset.UtcNow;
        var jobIds = applications
            .Select(x => x.JobId)
            .Distinct()
            .ToArray();
        var tagsByApplicationId = await GetTagsByApplicationIdAsync(
            db,
            applications.Select(x => x.Id).ToArray(),
            cancellationToken);

        var jobTitles = await db.Jobs
            .Where(x => jobIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);

        var items = applications
            .Where(x => jobTitles.ContainsKey(x.JobId))
            .Select(x => ToRecruiterApplicationResponse(
                x,
                x.JobId,
                jobTitles[x.JobId],
                nowUtc,
                tagsByApplicationId.GetValueOrDefault(x.Id, [])))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToArray();

        return TypedResults.Ok(new RecruiterBulkStatusMoveResponse(
            applicationIds.Length,
            movedCount,
            request.Status,
            items));
    }

    private static async Task<Results<Ok<RecruiterBulkTagUpdateResponse>, ProblemHttpResult>> BulkUpdateApplicationTagsAsync(
        BulkUpdateApplicationTagsRequest request,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var applicationIds = request.ApplicationIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        var normalizedTags = request.Tags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(ApplicationTag.Normalize)
            .Distinct()
            .ToArray();

        if (applicationIds.Length == 0)
        {
            return TypedResults.Problem(
                title: "Applications required",
                detail: "Select at least one candidate card before applying bulk tags.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (normalizedTags.Length == 0)
        {
            return TypedResults.Problem(
                title: "Tags required",
                detail: "Add at least one tag before running the bulk tag action.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!IsSupportedTagOperation(request.Operation))
        {
            return TypedResults.Problem(
                title: "Unsupported tag operation",
                detail: "Use Add or Remove when changing tags in bulk.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var applications = await db.Applications
            .Where(x => applicationIds.Contains(x.Id))
            .OrderByDescending(x => x.AppliedAtUtc)
            .ToArrayAsync(cancellationToken);

        var jobIds = applications
            .Select(x => x.JobId)
            .Distinct()
            .ToArray();

        var jobTitles = await db.Jobs
            .Where(x => jobIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);

        var existingTags = await db.ApplicationTags
            .Where(x => applicationIds.Contains(x.ApplicationId))
            .ToListAsync(cancellationToken);

        if (request.Operation == "Add")
        {
            var existingKeySet = existingTags
                .Select(x => $"{x.ApplicationId:N}:{x.Name}")
                .ToHashSet(StringComparer.Ordinal);

            foreach (var applicationId in applicationIds)
            {
                foreach (var tag in normalizedTags)
                {
                    var key = $"{applicationId:N}:{tag}";
                    if (!existingKeySet.Add(key))
                    {
                        continue;
                    }

                    db.ApplicationTags.Add(new ApplicationTag(
                        currentTenant.TenantId.Value,
                        applicationId,
                        tag));
                }
            }
        }
        else
        {
            var toRemove = existingTags
                .Where(x => normalizedTags.Contains(x.Name))
                .ToArray();

            db.ApplicationTags.RemoveRange(toRemove);
        }

        await db.SaveChangesAsync(cancellationToken);

        var tagsByApplicationId = await GetTagsByApplicationIdAsync(
            db,
            applications.Select(x => x.Id).ToArray(),
            cancellationToken);

        var nowUtc = DateTimeOffset.UtcNow;
        var items = applications
            .Where(x => jobTitles.ContainsKey(x.JobId))
            .Select(x => ToRecruiterApplicationResponse(
                x,
                x.JobId,
                jobTitles[x.JobId],
                nowUtc,
                tagsByApplicationId.GetValueOrDefault(x.Id, [])))
            .ToArray();

        return TypedResults.Ok(new RecruiterBulkTagUpdateResponse(
            applicationIds.Length,
            request.Operation,
            normalizedTags,
            items));
    }

    private static async Task<Results<Ok<RecruiterApplicationFilterPresetResponse>, ProblemHttpResult>> SaveApplicationFilterPresetAsync(
        SaveRecruiterApplicationFilterPresetRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.Problem(
                title: "Preset name required",
                detail: "Give the saved filter a short name so recruiters can reuse it later.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !IsSupportedFilterStatus(request.Status))
        {
            return TypedResults.Problem(
                title: "Unsupported filter status",
                detail: "Use Applied, Interviewing, OfferSent, Hired, or Rejected.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var currentSubject = ResolveActorSubject(user);
        if (currentSubject is null)
        {
            return TypedResults.Problem(
                title: "Recruiter identity required",
                detail: "Saved filters require a stable authenticated recruiter subject.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var normalizedTag = NormalizeSearchValue(request.Tag);

        RecruiterApplicationFilterPreset preset;
        if (request.PresetId is Guid presetId && presetId != Guid.Empty)
        {
            preset = await db.RecruiterApplicationFilterPresets
                .SingleOrDefaultAsync(x => x.Id == presetId && x.CreatedByAuthSubject == currentSubject, cancellationToken)
                ?? new RecruiterApplicationFilterPreset(
                    currentTenant.TenantId.Value,
                    currentSubject,
                    request.Name,
                    request.Search,
                    request.Status,
                    normalizedTag,
                    request.Location,
                    request.StuckOnly);

            if (preset.Id == presetId)
            {
                preset.UpdateCriteria(
                    request.Name,
                    request.Search,
                    request.Status,
                    normalizedTag,
                    request.Location,
                    request.StuckOnly);
            }
            else
            {
                db.RecruiterApplicationFilterPresets.Add(preset);
            }
        }
        else
        {
            preset = await db.RecruiterApplicationFilterPresets
                .SingleOrDefaultAsync(
                    x => x.CreatedByAuthSubject == currentSubject && x.Name == request.Name.Trim(),
                    cancellationToken)
                ?? new RecruiterApplicationFilterPreset(
                    currentTenant.TenantId.Value,
                    currentSubject,
                    request.Name,
                    request.Search,
                    request.Status,
                    normalizedTag,
                    request.Location,
                    request.StuckOnly);

            if (db.Entry(preset).State == EntityState.Detached)
            {
                db.RecruiterApplicationFilterPresets.Add(preset);
            }
            else
            {
                preset.UpdateCriteria(
                    request.Name,
                    request.Search,
                    request.Status,
                    normalizedTag,
                    request.Location,
                    request.StuckOnly);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RecruiterApplicationFilterPresetResponse(
            preset.Id,
            preset.Name,
            preset.Search,
            preset.Status,
            preset.Tag,
            preset.Location,
            preset.StuckOnly,
            preset.CreatedAtUtc,
            preset.UpdatedAtUtc));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> DeleteApplicationFilterPresetAsync(
        Guid filterPresetId,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var currentSubject = ResolveActorSubject(user);
        if (currentSubject is null)
        {
            return TenantRequired();
        }

        var preset = await db.RecruiterApplicationFilterPresets
            .SingleOrDefaultAsync(x => x.Id == filterPresetId && x.CreatedByAuthSubject == currentSubject, cancellationToken);
        if (preset is null)
        {
            return TypedResults.NotFound();
        }

        db.RecruiterApplicationFilterPresets.Remove(preset);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<RecruiterCandidateActivityResponse>, ProblemHttpResult>> GetCandidateActivityAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var items = await db.ApplicationTimelineEvents
            .OrderByDescending(x => x.OccurredAtUtc)
            .Join(
                db.Applications,
                timeline => timeline.ApplicationId,
                application => application.Id,
                (timeline, application) => new { Timeline = timeline, Application = application })
            .Join(
                db.Jobs,
                joined => joined.Application.JobId,
                job => job.Id,
                (joined, job) => new RecruiterCandidateActivityItemResponse(
                    joined.Timeline.Id,
                    joined.Application.CandidateName,
                    joined.Application.CandidateEmail,
                    joined.Application.Id,
                    job.Id,
                    job.Title,
                    job.Slug,
                    joined.Timeline.EventType.ToString(),
                    joined.Timeline.Title,
                    joined.Timeline.Description,
                    joined.Timeline.ActorLabel,
                    joined.Application.Status.ToString(),
                    joined.Timeline.OccurredAtUtc))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new RecruiterCandidateActivityResponse(items));
    }

    private static async Task<Results<Ok<ApplicationTimelineResponse>, NotFound, ProblemHttpResult>> GetApplicationTimelineAsync(
        Guid applicationId,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var application = await db.Applications
            .Where(x => x.Id == applicationId)
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
            .Where(x => x.ApplicationId == applicationId)
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

    private static async Task<Results<Ok<TimelineEventResponse>, NotFound, ProblemHttpResult>> AddApplicationTimelineCommentAsync(
        Guid applicationId,
        AddApplicationTimelineCommentRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            return TypedResults.Problem(
                title: "Comment required",
                detail: "Add a note before posting to the timeline.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var application = await db.Applications
            .SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return TypedResults.NotFound();
        }

        var actorLabel =
            user.FindFirstValue("name") ??
            user.FindFirstValue(ClaimTypes.Email) ??
            "Recruiter operations";

        var timelineEvent = new ApplicationTimelineEvent(
            currentTenant.TenantId.Value,
            application.Id,
            application.CandidateProfileId,
            ApplicationTimelineEventType.NoteAdded,
            request.VisibleToCandidate
                ? ApplicationTimelineAudience.CandidateAndRecruiter
                : ApplicationTimelineAudience.RecruiterOnly,
            request.VisibleToCandidate ? "Recruiter update" : "Internal recruiter note",
            request.Comment,
            actorLabel);

        db.ApplicationTimelineEvents.Add(timelineEvent);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new TimelineEventResponse(
            timelineEvent.Id,
            timelineEvent.EventType.ToString(),
            timelineEvent.Audience.ToString(),
            timelineEvent.Title,
            timelineEvent.Description,
            timelineEvent.ActorLabel,
            timelineEvent.OccurredAtUtc));
    }

    private static async Task<Results<Ok<RecruiterInvoiceReadyResponse>, ProblemHttpResult>> GetInvoiceReadyAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var items = await db.Timesheets
            .Include(x => x.Entries)
            .Where(x => x.Status == TimesheetStatus.Approved)
            .OrderByDescending(x => x.ReviewedAtUtc ?? x.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new RecruiterInvoiceReadyResponse(ToInvoiceReadyItems(items)));
    }

    private static async Task<Results<Ok<RecruiterInvoiceHandoffResponse>, ProblemHttpResult>> GetInvoiceHandoffAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var approvedTimesheets = await db.Timesheets
            .Include(x => x.Entries)
            .Where(x => x.Status == TimesheetStatus.Approved)
            .OrderByDescending(x => x.ReviewedAtUtc ?? x.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var invoiceReadyItems = ToInvoiceReadyItems(approvedTimesheets);
        var batchReference = BuildBatchReference(currentTenant.TenantId.Value);

        return TypedResults.Ok(new RecruiterInvoiceHandoffResponse(
            $"qbo-timesheets-{DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.csv",
            invoiceReadyItems.Length,
            invoiceReadyItems.Sum(x => x.PayableHours),
            new RecruiterStripeFallbackBatchResponse(
                batchReference,
                invoiceReadyItems.Select(item => new RecruiterStripeFallbackItemResponse(
                    item.TimesheetId,
                    item.ContractorEmail,
                    $"Approved time for week of {item.WeekStartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
                    item.PayableHours,
                    "hours",
                    "send_invoice"))
                .ToArray())));
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> DownloadQuickBooksCsvAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var approvedTimesheets = await db.Timesheets
            .Include(x => x.Entries)
            .Where(x => x.Status == TimesheetStatus.Approved)
            .OrderByDescending(x => x.ReviewedAtUtc ?? x.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var invoiceReadyItems = ToInvoiceReadyItems(approvedTimesheets);
        var batchReference = BuildBatchReference(currentTenant.TenantId.Value);

        var csv = new StringBuilder();
        csv.AppendLine("batch_reference,timesheet_id,contractor_email,week_start_utc,approved_at_utc,regular_hours,overtime_hours,paid_time_off_hours,payable_hours,description");
        foreach (var item in invoiceReadyItems)
        {
            csv.Append(batchReference).Append(',')
                .Append(item.TimesheetId).Append(',')
                .Append(EscapeCsv(item.ContractorEmail)).Append(',')
                .Append(item.WeekStartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(item.ApprovedAtUtc?.ToString("O") ?? string.Empty).Append(',')
                .Append(item.RegularHours).Append(',')
                .Append(item.OvertimeHours).Append(',')
                .Append(item.PaidTimeOffHours).Append(',')
                .Append(item.PayableHours).Append(',')
                .Append(EscapeCsv($"Approved time for week of {item.WeekStartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"))
                .AppendLine();
        }

        return TypedResults.File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv; charset=utf-8",
            $"qbo-timesheets-{DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.csv");
    }

    private static async Task<Results<Ok<RecruiterApplicationResponse>, NotFound, ProblemHttpResult>> UpdateApplicationStatusAsync(
        Guid applicationId,
        UpdateApplicationStatusRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var application = await db.Applications
            .SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return TypedResults.NotFound();
        }

        if (!IsSupportedStatus(request.Status))
        {
            return TypedResults.Problem(
                title: "Unsupported application status",
                detail: "Use Interviewing, OfferSent, Hired, or Rejected.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ApplyStatusTransition(application, request.Status);
        db.ApplicationTimelineEvents.Add(CreateStageTimelineEvent(
            currentTenant.TenantId.Value,
            application,
            ResolveActorLabel(user)));
        await db.SaveChangesAsync(cancellationToken);

        var job = await db.Jobs.SingleAsync(x => x.Id == application.JobId, cancellationToken);
        var tagsByApplicationId = await GetTagsByApplicationIdAsync(
            db,
            [application.Id],
            cancellationToken);
        return TypedResults.Ok(ToRecruiterApplicationResponse(
            application,
            job.Id,
            job.Title,
            DateTimeOffset.UtcNow,
            tagsByApplicationId.GetValueOrDefault(application.Id, [])));
    }

    private static RecruiterJobResponse ToJobResponse(Job job) =>
        new(job.Id, job.Title, job.Slug, job.Location, job.Summary, job.Description, job.PostedOnUtc, job.IsPublished);

    private static RecruiterApplicationResponse ToRecruiterApplicationResponse(
        Application application,
        Guid jobId,
        string jobTitle,
        DateTimeOffset nowUtc,
        string[] tags)
    {
        var daysInStage = Math.Max(0, (int)Math.Floor((nowUtc - application.UpdatedAtUtc).TotalDays));

        return new RecruiterApplicationResponse(
            application.Id,
            jobId,
            jobTitle,
            application.CandidateName,
            application.CandidateEmail,
            application.Note,
            application.Status.ToString(),
            application.AppliedAtUtc,
            application.UpdatedAtUtc,
            daysInStage,
            daysInStage >= StuckThreshold.TotalDays,
            tags);
    }

    private static RecruiterInvoiceReadyItemResponse[] ToInvoiceReadyItems(IEnumerable<Timesheet> timesheets) =>
        timesheets.Select(x =>
        {
            var totals = x.CalculateTotals();
            return new RecruiterInvoiceReadyItemResponse(
                x.Id,
                x.ContractorEmail,
                x.WeekStartUtc,
                x.ReviewedAtUtc,
                totals.RegularHours,
                totals.OvertimeHours,
                totals.PaidTimeOffHours,
                totals.PayableHours);
        }).ToArray();

    private static string BuildBatchReference(Guid tenantId) =>
        $"tenant-{tenantId.ToString("N")[..8]}-{DateTime.UtcNow:yyyyMMdd}";

    private static ProblemHttpResult TenantRequired() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            detail: "Recruiter workflow requires a tenant_id claim in the authenticated session.",
            statusCode: StatusCodes.Status412PreconditionFailed);

    private static bool IsSupportedStatus(string status) =>
        status is "Interviewing" or "OfferSent" or "Hired" or "Rejected";

    private static bool IsSupportedFilterStatus(string status) =>
        status is "Applied" or "Interviewing" or "OfferSent" or "Hired" or "Rejected";

    private static bool IsSupportedTagOperation(string operation) =>
        operation is "Add" or "Remove";

    private static string? NormalizeSearchValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ApplyStatusTransition(Application application, string status)
    {
        switch (status)
        {
            case "Interviewing":
                application.TransitionToInterviewing();
                break;
            case "OfferSent":
                application.TransitionToOfferSent();
                break;
            case "Hired":
                application.TransitionToHired();
                break;
            case "Rejected":
                application.TransitionToRejected();
                break;
        }
    }

    private static string ResolveActorLabel(ClaimsPrincipal user) =>
        user.FindFirstValue("name") ??
        user.FindFirstValue(ClaimTypes.Email) ??
        "Recruiter operations";

    private static string? ResolveActorSubject(ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ??
        user.FindFirstValue(ClaimTypes.NameIdentifier);

    private static async Task<Dictionary<Guid, string[]>> GetTagsByApplicationIdAsync(
        AppDbContext db,
        Guid[] applicationIds,
        CancellationToken cancellationToken)
    {
        if (applicationIds.Length == 0)
        {
            return [];
        }

        var tags = await db.ApplicationTags
            .AsNoTracking()
            .Where(x => applicationIds.Contains(x.ApplicationId))
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);

        return tags
            .GroupBy(x => x.ApplicationId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.Name).ToArray());
    }

    private static ApplicationTimelineEvent CreateStageTimelineEvent(
        Guid tenantId,
        Application application,
        string actorLabel) =>
        new(
            tenantId,
            application.Id,
            application.CandidateProfileId,
            ApplicationTimelineEventType.StageChanged,
            ApplicationTimelineAudience.CandidateAndRecruiter,
            BuildStageTimelineTitle(application.Status),
            BuildStageTimelineDetail(application.Status),
            actorLabel,
            application.UpdatedAtUtc);

    private static string BuildStageTimelineTitle(ApplicationStatus status) =>
        status switch
        {
            ApplicationStatus.Interviewing => "Moved to interviewing",
            ApplicationStatus.OfferSent => "Offer prepared",
            ApplicationStatus.Hired => "Marked hired",
            ApplicationStatus.Rejected => "Application closed",
            _ => "Application updated",
        };

    private static string BuildStageTimelineDetail(ApplicationStatus status) =>
        status switch
        {
            ApplicationStatus.Interviewing => "The application progressed from screening into the interview stage.",
            ApplicationStatus.OfferSent => "The recruiting team prepared and delivered the offer package.",
            ApplicationStatus.Hired => "The candidate accepted and was moved into the hired stage.",
            ApplicationStatus.Rejected => "The recruiting team closed the application after review.",
            _ => "The application timeline was updated.",
        };

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        return string.Join(
            '-',
            new string(chars)
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}

public sealed record UpsertRecruiterJobRequest(
    string Title,
    string Location,
    string Summary,
    string Description,
    bool IsPublished,
    DateOnly? PostedOnUtc);

public sealed record UpdateApplicationStatusRequest(string Status);

public sealed record BulkUpdateApplicationStatusRequest(
    Guid[] ApplicationIds,
    string Status);

public sealed record BulkUpdateApplicationTagsRequest(
    Guid[] ApplicationIds,
    string[] Tags,
    string Operation);

public sealed record SaveRecruiterApplicationFilterPresetRequest(
    Guid? PresetId,
    string Name,
    string? Search,
    string? Status,
    string? Tag,
    string? Location,
    bool StuckOnly);

public sealed class RecruiterApplicationQuery
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Tag { get; init; }
    public string? Location { get; init; }
    public bool? StuckOnly { get; init; }
}

public sealed record RecruiterJobResponse(
    Guid Id,
    string Title,
    string Slug,
    string Location,
    string Summary,
    string Description,
    DateOnly PostedOnUtc,
    bool IsPublished);

public sealed record RecruiterApplicationResponse(
    Guid Id,
    Guid JobId,
    string JobTitle,
    string CandidateName,
    string CandidateEmail,
    string? Note,
    string Status,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int DaysInStage,
    bool IsStuck,
    string[] Tags);

public sealed record RecruiterApplicationsBoardResponse(
    RecruiterApplicationResponse[] Items,
    string[] AvailableTags,
    string[] AvailableLocations,
    RecruiterApplicationFilterPresetResponse[] SavedFilters);

public sealed record RecruiterBulkStatusMoveResponse(
    int RequestedCount,
    int UpdatedCount,
    string Status,
    RecruiterApplicationResponse[] Items);

public sealed record RecruiterBulkTagUpdateResponse(
    int RequestedCount,
    string Operation,
    string[] Tags,
    RecruiterApplicationResponse[] Items);

public sealed record RecruiterApplicationFilterPresetResponse(
    Guid Id,
    string Name,
    string? Search,
    string? Status,
    string? Tag,
    string? Location,
    bool StuckOnly,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record RecruiterInvoiceReadyResponse(RecruiterInvoiceReadyItemResponse[] Items);

public sealed record RecruiterInvoiceHandoffResponse(
    string QuickBooksFileName,
    int ApprovedTimesheetCount,
    decimal TotalPayableHours,
    RecruiterStripeFallbackBatchResponse StripeFallback);

public sealed record RecruiterStripeFallbackBatchResponse(
    string BatchReference,
    RecruiterStripeFallbackItemResponse[] Items);

public sealed record RecruiterStripeFallbackItemResponse(
    Guid TimesheetId,
    string ContractorEmail,
    string Description,
    decimal Quantity,
    string Unit,
    string CollectionMethod);

public sealed record RecruiterInvoiceReadyItemResponse(
    Guid TimesheetId,
    string ContractorEmail,
    DateOnly WeekStartUtc,
    DateTimeOffset? ApprovedAtUtc,
    decimal RegularHours,
    decimal OvertimeHours,
    decimal PaidTimeOffHours,
    decimal PayableHours);
