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
            BuildQuantamJobSeeds(),
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
            BuildQuantamCandidateSeeds(),
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

        // Spread the candidate pool across the application funnel so the
        // recruiter board, the reporting summary, and the client portal
        // each render with non-trivial content. Distribution roughly:
        //   ~55% Applied · 20% Interviewing · 10% OfferSent · 5% Hired · 10% Rejected
        // Round-robins candidates against jobs so multiple jobs have apps.
        var rng = new Random(42); // deterministic seed — same data every boot
        for (var i = 0; i < profiles.Length; i++)
        {
            var profile = profiles[i];
            var job = jobs[i % jobs.Length];

            if (existing.Any(x => x.JobId == job.Id && x.CandidateProfileId == profile.Id))
            {
                continue;
            }

            var application = new Application(
                tenantId,
                job.Id,
                profile.Id,
                profile.Email,
                profile.FullName ?? $"Candidate {i + 1}",
                rng.Next(2) == 0 ? null : "Recruiter screen scheduled this week.");

            // Pick a stage by drawing against the rough distribution above.
            var roll = rng.NextDouble();
            if (roll < 0.55) { /* stay in Applied */ }
            else if (roll < 0.75) { application.TransitionToInterviewing(); }
            else if (roll < 0.85)
            {
                application.TransitionToInterviewing();
                application.TransitionToOfferSent();
            }
            else if (roll < 0.90)
            {
                application.TransitionToInterviewing();
                application.TransitionToOfferSent();
                application.TransitionToHired();
            }
            else
            {
                application.TransitionToRejected();
            }

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

    /// <summary>
    /// Twelve realistic staffing roles spread across cloud, data, security,
    /// platform, and delivery — enough variety that the recruiter board,
    /// the public jobs page, and the Indeed feed all render meaningful
    /// content out of the box.
    /// </summary>
    private static SeedJob[] BuildQuantamJobSeeds()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return
        [
            new SeedJob("Senior .NET Staffing Solutions Lead", "senior-dotnet-staffing-solutions-lead",
                "Remote · United States",
                "Own recruiter collaboration, client intake, and candidate workflow design for a growing staffing operation.",
                "Lead the shaping of recruiter-facing workflow inside a modern staffing platform. Partner with delivery leadership, turn recruiting process pain into software requirements, and operationalize better candidate submission velocity.",
                today.AddDays(-1)),
            new SeedJob("Technical Recruiter — Cloud & Data", "technical-recruiter-cloud-and-data",
                "Dallas, TX · Hybrid",
                "Drive sourcing and screening for cloud, data, and engineering roles while improving reusable search playbooks.",
                "Join a staffing team focused on cloud and data talent. Hands-on sourcing, hiring-manager calibration, candidate storytelling, and lightweight process improvement across the submission funnel.",
                today.AddDays(-2)),
            new SeedJob("Senior Cloud Platform Engineer", "senior-cloud-platform-engineer",
                "Austin, TX · Remote",
                "Build and operate the platform team's Kubernetes-on-Azure footprint serving multiple lines of business.",
                "Own the shared platform that hosts our internal services. Multi-tenant Kubernetes, GitOps with ArgoCD, observability with Application Insights and OpenTelemetry, and a focus on developer experience.",
                today.AddDays(-3)),
            new SeedJob("Staff Data Engineer", "staff-data-engineer",
                "New York, NY · Hybrid",
                "Design and own the staffing operations data warehouse from ingestion through self-serve analytics.",
                "We're building a single source of truth for placement metrics, recruiter activity, and client engagement. Lead the dbt + Snowflake architecture, coach two senior engineers, and partner with revenue ops.",
                today.AddDays(-4)),
            new SeedJob("Security Engineering Manager", "security-engineering-manager",
                "Remote · United States",
                "Lead a small AppSec / DevSecOps team operating across our SaaS portfolio.",
                "Manage two engineers running threat modeling, SAST/DAST integration, and SOC 2 readiness work. Enterprise customers in flight; you'll partner with auditors and our compliance lead through their first Type II audit.",
                today.AddDays(-5)),
            new SeedJob("Senior Frontend Engineer — Angular", "senior-frontend-engineer-angular",
                "Remote · North America",
                "Own the recruiter portal SPA — Angular, signals, accessibility-first design.",
                "Build the recruiter and client portals on Angular 21 with signals + standalone components. WCAG 2.1 AA target, design system shared across portals, deep partnership with the staffing operations team.",
                today.AddDays(-6)),
            new SeedJob("DevOps Engineer", "devops-engineer",
                "Phoenix, AZ · On-site",
                "Keep the deploy pipeline boring — GitHub Actions, Bicep, Container Apps, managed Postgres.",
                "Own a single deploy pipeline that ships API + SPA together every merge. Bicep + GHA + ACA + Neon Postgres. Help bring private GHCR + managed-identity pull online before our first paying customer.",
                today.AddDays(-7)),
            new SeedJob("Salesforce Administrator", "salesforce-administrator",
                "Chicago, IL · Hybrid",
                "Own our Salesforce org — recruiter pipelines, client deal flow, billing handoff to QuickBooks.",
                "Configure, automate, and report. Partner with revenue ops to keep the staffing pipeline measurable and the recruiter experience uncluttered. Salesforce Admin certified is a strong signal.",
                today.AddDays(-9)),
            new SeedJob("Healthcare Staffing Recruiter", "healthcare-staffing-recruiter",
                "Atlanta, GA · Hybrid",
                "Source, screen, and submit clinicians for travel and contract roles across the southeast.",
                "Run a healthcare desk with a focus on travel nursing and allied health. Hospital networks, credentialing know-how, and a calm bedside manner with both candidates and clinicians required.",
                today.AddDays(-10)),
            new SeedJob("ERP Staffing Account Executive", "erp-staffing-account-executive",
                "Remote · United States",
                "Grow ERP contract business while partnering with recruiters on sharper submittal quality.",
                "Help our staffing team expand its ERP footprint. Run client conversations, refine intake, and close the loop between account strategy and candidate delivery — SAP, Oracle, Workday, or Dynamics experience welcome.",
                today.AddDays(-12)),
            new SeedJob("Technical Program Manager — Onboarding", "tpm-onboarding",
                "San Francisco, CA · Hybrid",
                "Run the cross-functional onboarding workstream for new tenants signing up for the platform.",
                "Coordinate engineering, product, customer success, and security around tenant onboarding milestones. Strong written communication and a track record shipping cross-team rollouts in B2B SaaS.",
                today.AddDays(-14)),
            new SeedJob("Senior Backend Engineer — Payroll", "senior-backend-engineer-payroll",
                "Remote · United States",
                "Build the payroll-handoff service that turns approved timesheets into pay batches.",
                "Greenfield service in C# / EF Core / Postgres. You'll design the QuickBooks + Stripe handoff, partner with finance, and help define the evolution of the contractor portal.",
                today.AddDays(-15)),
        ];
    }

    /// <summary>
    /// Thirty candidate profiles with varied roles + locations so the
    /// recruiter board / candidate dashboard / submissions queue all
    /// render with meaningful content.
    /// </summary>
    private static SeedCandidate[] BuildQuantamCandidateSeeds()
    {
        // Static fixture — the auth_subject pattern + email pattern keep the
        // upsert idempotent across restarts. Headlines + summaries are all
        // synthetic; no real-person attribution.
        var samples = new (string FullName, string Headline, string Summary, string Phone)[]
        {
            ("Jane Candidate",       "Cloud recruiter",                       "Five years sourcing senior platform engineers across Azure-shop teams.", "555 010 1001"),
            ("Marcus Sourcer",       "Data sourcer",                          "Hybrid sourcing and coordination experience across analytics and ML roles.", "555 010 1002"),
            ("Alicia Reyes",         "Senior Cloud Engineer",                 "AWS + Azure dual-platform background, comfortable with Kubernetes operator development.", "555 010 1003"),
            ("Daniel Park",          "Staff Frontend Engineer",               "Angular and React expert; led design-system rollouts at two scaleups.", "555 010 1004"),
            ("Priya Sharma",         "Security Engineer",                     "OWASP-focused AppSec engineer with DAST/SAST integration experience.", "555 010 1005"),
            ("Jordan Williams",      "Data Engineer",                         "dbt + Snowflake expert, ran the data layer of a healthcare-staffing platform.", "555 010 1006"),
            ("Emma Chen",            "Engineering Manager",                   "Manager of a 6-engineer platform team; SOC 2 + security-review track record.", "555 010 1007"),
            ("Brandon Hayes",        "Senior Backend Engineer",               ".NET / Postgres specialist with payroll-domain familiarity.", "555 010 1008"),
            ("Yasmin Patel",         "DevOps Engineer",                       "Bicep + GitHub Actions specialist, ACA / AKS at production scale.", "555 010 1009"),
            ("Tomasz Nowak",         "Lead Mobile Engineer",                  "iOS + Android with React Native; consumer scale-up experience.", "555 010 1010"),
            ("Hannah Davis",         "TPM — Onboarding",                      "Coordinated launches for B2B SaaS rollouts at three companies.", "555 010 1011"),
            ("Carlos Mendez",        "Salesforce Administrator",              "Admin Cert + reporting wizard for staffing and revenue-ops orgs.", "555 010 1012"),
            ("Olivia Brooks",        "Healthcare Recruiter",                  "Travel nursing + allied health; comfortable with credentialing workflows.", "555 010 1013"),
            ("Samir Rao",            "ERP Account Executive",                 "Eight years selling SAP and Oracle staffing into mid-market manufacturing.", "555 010 1014"),
            ("Megan O'Connor",       "Senior QA Engineer",                    "Cypress + Playwright, accessibility audits, and CI gating expertise.", "555 010 1015"),
            ("Ethan Kapoor",         "ML Engineer",                           "Recommender systems + LLM productionization in a staffing-marketplace context.", "555 010 1016"),
            ("Sofia Russo",          "Senior Designer",                       "Design system lead with WCAG 2.1 AA + government-style work history.", "555 010 1017"),
            ("Luis Castillo",        "Cloud Architect",                       "Azure CAF / WAF reviews; landing-zone deployments at enterprise scale.", "555 010 1018"),
            ("Aisha Bello",          "Database Engineer",                     "Postgres performance tuning + sharding strategies for multi-tenant SaaS.", "555 010 1019"),
            ("Nathan Greene",        "Site Reliability Engineer",             "Pager veteran with on-call playbook + post-mortem culture experience.", "555 010 1020"),
            ("Hina Ahmed",           "Product Manager",                       "B2B SaaS PM with staffing-domain experience and strong written specs.", "555 010 1021"),
            ("Ben Carter",           "Senior Recruiter",                      "Quota-carrying recruiter for cloud and security roles in financial services.", "555 010 1022"),
            ("Riya Iyer",            "Compensation Analyst",                  "Banded comp design, equity refresh modeling, and comp benchmarking.", "555 010 1023"),
            ("Dylan Foster",         "Solutions Architect",                   "Pre-sales + post-sales architect for analytics platforms; Azure cert stack.", "555 010 1024"),
            ("Anna Schmidt",         "Customer Success Lead",                 "Onboarding playbooks + renewal-rate ownership in B2B SaaS.", "555 010 1025"),
            ("Pavan Krishnan",       "FullStack Engineer",                    ".NET / Angular fullstack with shipping experience to enterprise clients.", "555 010 1026"),
            ("Jasmine Wells",        "Talent Operations Lead",                "ATS implementation + recruiter-tooling rollouts across global teams.", "555 010 1027"),
            ("Kenji Tanaka",         "Senior iOS Engineer",                   "Swift + SwiftUI; design-systems-on-mobile at consumer-scale apps.", "555 010 1028"),
            ("Zara Khan",            "AppSec Engineer",                       "Threat-modeling lead + bug-bounty triage at a fintech.", "555 010 1029"),
            ("Marco Rossi",          "Engineering Director",                  "Scaled three engineering orgs from 10 to 60+ in B2B SaaS; PE-backed exposure.", "555 010 1030"),
        };

        var seeds = new List<SeedCandidate>(samples.Length);
        var idx = 0;
        foreach (var (name, headline, summary, phone) in samples)
        {
            idx++;
            var slug = name.ToLowerInvariant().Replace(' ', '.').Replace("'", "");
            seeds.Add(new SeedCandidate(
                AuthSubject: $"seed|candidate-{idx}",
                Email: $"{slug}@example.com",
                FullName: name,
                PhoneNumber: $"+1 {phone}",
                Headline: headline,
                Summary: summary));
        }
        return seeds.ToArray();
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
