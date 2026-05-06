using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;

namespace QuantamAnalytics.Api.Demo;

public sealed class DemoDataSeeder
{
    private readonly AppDbContext _db;

    public DemoDataSeeder(AppDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var quantam = await EnsureTenantAsync("quantam", "Quantam Analytics", cancellationToken);
        var skyline = await EnsureTenantAsync("skyline", "Skyline Talent Group", cancellationToken);

        var quantamJobs = await EnsureJobsAsync(
            quantam,
            [
                new SeedJob(
                    "Senior .NET Staffing Solutions Lead",
                    "senior-dotnet-staffing-solutions-lead",
                    "Remote · United States",
                    "Own recruiter collaboration, client intake, and candidate workflow design for a growing staffing operation.",
                    "Lead the shaping of recruiter-facing workflow inside a modern staffing platform. You will partner with delivery leadership, turn recruiting process pain into software requirements, and help operationalize better candidate submission velocity.",
                    DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-4))),
                new SeedJob(
                    "Technical Recruiter - Cloud & Data",
                    "technical-recruiter-cloud-and-data",
                    "Dallas, TX · Hybrid",
                    "Drive sourcing and screening for cloud, data, and engineering roles while improving reusable search playbooks.",
                    "Join a staffing team focused on cloud and data talent. This role blends hands-on sourcing, hiring-manager calibration, candidate storytelling, and lightweight process improvement across the submission funnel.",
                    DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1))),
            ],
            cancellationToken);

        await EnsureJobsAsync(
            skyline,
            [
                new SeedJob(
                    "Healthcare Staffing Delivery Manager",
                    "healthcare-staffing-delivery-manager",
                    "Atlanta, GA · Hybrid",
                    "Coordinate clinicians, recruiters, and client delivery timelines for a fast-moving healthcare desk.",
                    "Own day-to-day delivery rhythm for healthcare staffing placements. This role balances recruiter enablement, client communication, and funnel visibility across multiple openings.",
                    DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))),
                new SeedJob(
                    "ERP Staffing Account Executive",
                    "erp-staffing-account-executive",
                    "Chicago, IL · Remote",
                    "Grow ERP contract business while partnering with recruiters on sharper submittal quality.",
                    "Help a staffing team expand its ERP footprint by running client conversations, refining intake, and closing the loop between account strategy and candidate delivery.",
                    DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-2))),
            ],
            cancellationToken);

        var quantamProfiles = await EnsureCandidateProfilesAsync(
            quantam.Id,
            [
                new SeedCandidate(
                    "seed|candidate-1",
                    "candidate.one@example.com",
                    "Jane Candidate",
                    "+1 555 010 1001",
                    "Cloud recruiter",
                    "Interested in platform recruiting roles."),
                new SeedCandidate(
                    "seed|candidate-2",
                    "candidate.two@example.com",
                    "Marcus Sourcer",
                    "+1 555 010 1002",
                    "Data sourcer",
                    "Brings hybrid sourcing and coordination experience."),
            ],
            cancellationToken);

        await EnsureApplicationsAsync(quantam.Id, quantamJobs, quantamProfiles, cancellationToken);
        await EnsureSubmissionsAsync(quantam.Id, cancellationToken);
        await EnsureInterviewEventsAsync(quantam.Id, cancellationToken);
    }

    private async Task<Tenant> EnsureTenantAsync(
        string slug,
        string name,
        CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.SingleOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (tenant is not null)
        {
            return tenant;
        }

        tenant = new Tenant(slug, name);
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    private async Task<Job[]> EnsureJobsAsync(
        Tenant tenant,
        SeedJob[] jobs,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Jobs
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenant.Id)
            .ToListAsync(cancellationToken);

        foreach (var seed in jobs)
        {
            if (existing.Any(x => x.Slug == seed.Slug))
            {
                continue;
            }

            var job = new Job(
                tenant.Id,
                seed.Title,
                seed.Slug,
                seed.Location,
                seed.Summary,
                seed.Description,
                seed.PostedOnUtc);

            _db.Jobs.Add(job);
            existing.Add(job);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return existing.OrderByDescending(x => x.PostedOnUtc).ToArray();
    }

    private async Task<CandidateProfile[]> EnsureCandidateProfilesAsync(
        Guid tenantId,
        SeedCandidate[] candidates,
        CancellationToken cancellationToken)
    {
        var existing = await _db.CandidateProfiles
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        foreach (var seed in candidates)
        {
            if (existing.Any(x => x.AuthSubject == seed.AuthSubject))
            {
                continue;
            }

            var profile = new CandidateProfile(
                tenantId,
                seed.AuthSubject,
                seed.Email,
                seed.FullName);

            profile.UpdateProfile(
                seed.Email,
                seed.FullName,
                seed.PhoneNumber,
                seed.Headline,
                seed.Summary);

            _db.CandidateProfiles.Add(profile);
            existing.Add(profile);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return existing.ToArray();
    }

    private async Task EnsureApplicationsAsync(
        Guid tenantId,
        Job[] jobs,
        CandidateProfile[] profiles,
        CancellationToken cancellationToken)
    {
        if (jobs.Length == 0 || profiles.Length < 2)
        {
            return;
        }

        var existing = await _db.Applications
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (!existing.Any(x => x.JobId == jobs[0].Id && x.CandidateProfileId == profiles[0].Id))
        {
            _db.Applications.Add(new Application(
                tenantId,
                jobs[0].Id,
                profiles[0].Id,
                profiles[0].Email,
                profiles[0].FullName ?? "Jane Candidate",
                "Ready for a recruiter screening this week."));
        }

        var interviewingJob = jobs[Math.Min(1, jobs.Length - 1)];
        if (!existing.Any(x => x.JobId == interviewingJob.Id && x.CandidateProfileId == profiles[1].Id))
        {
            var application = new Application(
                tenantId,
                interviewingJob.Id,
                profiles[1].Id,
                profiles[1].Email,
                profiles[1].FullName ?? "Marcus Sourcer",
                "Strong fit for cloud and data coordination.");
            application.TransitionToInterviewing();
            _db.Applications.Add(application);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureInterviewEventsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var submissions = await _db.Submissions
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId &&
                (x.Status == SubmissionStatus.ClientReviewing || x.Status == SubmissionStatus.ClientAccepted))
            .OrderBy(x => x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var submission in submissions.Take(2))
        {
            if (await _db.InterviewEvents.IgnoreQueryFilters().AnyAsync(
                x => x.TenantId == tenantId && x.SubmissionId == submission.Id,
                cancellationToken))
            {
                continue;
            }

            var start = DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(15);
            if (_db.InterviewEvents.IgnoreQueryFilters().Any(x => x.TenantId == tenantId))
            {
                start = start.AddDays(1);
            }

            var provider = submission.Status == SubmissionStatus.ClientAccepted
                ? InterviewCalendarProvider.OutlookCalendar
                : InterviewCalendarProvider.GoogleCalendar;

            var interview = new InterviewEvent(
                tenantId,
                submission.Id,
                submission.CandidateName,
                submission.CandidateEmail,
                submission.Status == SubmissionStatus.ClientAccepted
                    ? "Client debrief and closeout"
                    : "Client screening panel",
                submission.Status == SubmissionStatus.ClientAccepted
                    ? "Delivery manager"
                    : "Hiring manager",
                provider,
                start,
                start.AddHours(1));

            interview.AttachProviderReference($"{provider.ToString().ToLowerInvariant()}-{submission.Id:N}"[..36]);
            _db.InterviewEvents.Add(interview);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSubmissionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var applications = await _db.Applications
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.AppliedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var application in applications.Take(2))
        {
            if (await _db.Submissions.IgnoreQueryFilters().AnyAsync(
                x => x.TenantId == tenantId && x.ApplicationId == application.Id,
                cancellationToken))
            {
                continue;
            }

            var submission = new Submission(
                tenantId,
                application.Id,
                application.JobId,
                application.CandidateProfileId,
                application.CandidateEmail,
                application.CandidateName);

            submission.SubmitToClient(
                "seed|recruiter-1",
                "Acme Client",
                "Strong recruiter-vetted candidate ready for client feedback.");

            if (application.Status == ApplicationStatus.Interviewing)
            {
                submission.MarkClientReviewing();
            }

            _db.Submissions.Add(submission);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeedJob(
        string Title,
        string Slug,
        string Location,
        string Summary,
        string Description,
        DateOnly PostedOnUtc);

    private sealed record SeedCandidate(
        string AuthSubject,
        string Email,
        string FullName,
        string PhoneNumber,
        string Headline,
        string Summary);
}
