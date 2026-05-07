# CLAUDE.md — Project Memory

This file is read by Claude on every session in this repo. Treat the rules below as non-negotiable.

## Project

**Quantam Analytics** — a multi-tenant staffing SaaS platform. Built first for one staffing firm, designed from day 1 to be sold as SaaS.

- Domain: `quantamanalitics.com`
- Owner: Shahnawaz Mohammed (`mshanawaztech@gmail.com`)
- GitHub: `mshanawaz114/quantamanalitics`
- Live dev SPA: `https://ambitious-dune-099500b0f.7.azurestaticapps.net`
- Live dev API: `https://ca-qa-dev-api.icymushroom-94be4003.eastus2.azurecontainerapps.io`

Roadmap: [`plan/plan.md`](./plan/plan.md). Per-phase boards: [`plan/phase-1-deliverables.md`](./plan/phase-1-deliverables.md), [`plan/phase-2-deliverables.md`](./plan/phase-2-deliverables.md), [`plan/phase-3-deliverables.md`](./plan/phase-3-deliverables.md), [`plan/phase-4-deliverables.md`](./plan/phase-4-deliverables.md), [`plan/phase-5-deliverables.md`](./plan/phase-5-deliverables.md).

**Phase 5 onward is structured as Epic → Story** rather than a flat PR list. Each story has explicit acceptance criteria + prerequisites (because most Phase 5 work depends on real partner-API credentials you have to set up before the PR can merge). Smoke / verification reference for everything shipped through Phase 4 + PR-44 lives in [`docs/verification.md`](./docs/verification.md) — first stop after any deploy.

## Stack (locked — do not change without an ADR)

- Backend: ASP.NET Core 10 LTS, EF Core 10
- Database: PostgreSQL (dev: Neon free tier; prod: Azure Database for PostgreSQL Flexible Server)
- Background jobs: Hangfire (in-process)
- Frontend: Angular 21, single SPA with route-based portals (recruiter / client / candidate / contractor)
- Auth: Auth0 free tier (tenant `quantamanalitics-dev.us.auth0.com`)
- File storage: Cloudflare R2 (S3-compatible)
- Hosting: Azure Container Apps (API), Azure Static Web Apps (web)
- Email: Resend (transactional), Cloudflare Email Routing (mailboxes)
- CI/CD: GitHub Actions (`ci.yml`, `deploy-dev.yml`)
- Infra-as-code: Bicep
- Observability: Application Insights

## Branch & PR rules — non-negotiable

1. **Never push to `main`.** `main` is protected and only accepts squash-merged PRs.
2. **Branch naming:** `qa001-<short-scope>` (e.g. `qa001-checkr-baseline`). The `qa001` prefix is permanent.
3. **One PR = one deliverable.** If a PR exceeds ~600 changed lines, split it.
4. **PR template required** — see `.github/pull_request_template.md`. Fill in every section.
5. **Squash-merge only.** Keeps `main` history linear.
6. **Commit messages:** `<type>(<scope>): <summary>`. Type ∈ `feat | fix | chore | docs | refactor | test | ci`. Body explains *why*, not *what*.
7. **Update the active phase's deliverables board after every merge.** Stale boards are a debugging tax we keep paying.

## Folder structure (current)

```
quantamanalitics/
├── api/
│   ├── Directory.Build.props
│   ├── QuantamAnalytics.sln
│   ├── Dockerfile
│   ├── QuantamAnalytics.Domain/
│   │   ├── Common/      ← IEntity, ITenantScoped, Roles, ClaimNamespace
│   │   └── Entities/    ← Tenant, Job, CandidateProfile, Application,
│   │                       Submission, InterviewEvent, BackgroundCheck,
│   │                       EsignDocument, OnboardingChecklistItem,
│   │                       AuditLogEntry, Timesheet, TimeEntry, TimesheetTotals
│   ├── QuantamAnalytics.Infrastructure/
│   │   ├── Data/        ← AppDbContext + per-entity Configurations/
│   │   │                 + AuditLogSaveChangesInterceptor (PR-44)
│   │   ├── Tenancy/     ← CurrentTenant, ICurrentTenant{,Setter}
│   │   ├── Storage/     ← R2 + DisabledResumeStorage fallback
│   │   ├── Interviews/  ← Calendar provider + meeting-link generator (stub)
│   │   ├── BackgroundChecks/ ← ICheckrClient + StubCheckrClient
│   │   ├── Esign/       ← IDocuSealClient + StubDocuSealClient
│   │   ├── JobBoards/   ← IDicePostingClient + StubDicePostingClient
│   │   └── Migrations/
│   ├── QuantamAnalytics.Api/
│   │   ├── Auth/        ← AuthExtensions, AuthorizationPolicies, StartupLog
│   │   ├── Tenancy/     ← Tenant resolver middleware
│   │   ├── Demo/        ← DemoDataSeeder (startup seed for repeatable previews)
│   │   └── Endpoints/   ← Health, Me, PublicJobs, JobFeed, RecruiterPortal,
│   │                       CandidateProfile, ContractorTimesheet,
│   │                       ClientApproval, ClientPortalJobs,
│   │                       ClientPortalDocuments, InterviewScheduling,
│   │                       BackgroundCheck, EsignDocument,
│   │                       OnboardingChecklist, DicePosting, Reporting,
│   │                       AuditLog
│   └── QuantamAnalytics.Tests/
│       ├── Fixtures/    ← PostgresFixture + IsolatedAppFactory (PR-30 IT pattern)
│       └── TestAuth/    ← TestAuthHandler for synthetic JWT integration tests
├── client/
│   └── src/app/
│       ├── core/        ← auth, health, jobs, candidate, recruiter,
│       │                  contractor, client, interviews services
│       ├── *.component.ts ← marketing-shell, home, about, services, contact,
│       │                     jobs-list, job-detail, candidate-dashboard,
│       │                     recruiter-dashboard, contractor-dashboard,
│       │                     client-dashboard, interview-scheduling, not-found
│       ├── app.routes.ts
│       ├── app.config.ts ← provideAuth0 + functional HTTP interceptor
│       └── environments/  ← swapped at prod build via fileReplacements
├── infra/                ← Bicep modules (rg-qa-dev)
├── plan/                 ← phase-1, phase-2, phase-3 deliverables + plan.md
├── docs/                 ← cicd.md, auth.md, ADRs
├── .github/              ← workflows (ci.yml, deploy-dev.yml), PR template
├── .claude/              ← project skills + settings
└── CLAUDE.md, README.md, CONTRIBUTING.md, LICENSE
```

## Things to NEVER do

- Never commit secrets (API keys, connection strings, tokens). `.env` files are gitignored — keep them that way.
- Never push directly to `main`. Always PR.
- Never disable EF Core global query filters in production code. Tenant isolation depends on them. Use `IgnoreQueryFilters()` only with a `// reason` comment in PlatformAdmin-scoped paths.
- Never store sensitive PII (SSN, DOB) without encryption-at-rest column types.
- Never accept a `tenant_id` from the client — always derive from the authenticated user's JWT via `ICurrentTenant`.
- Never bump a major dependency version without an ADR explaining why.
- Never skip the PR template.

## Things to ALWAYS do

- Run `dotnet format` and `npm run lint` before committing.
- Use the `qa001-` branch prefix.
- Reference the PR number in commit messages once a PR is open (`feat(jobs): list endpoint paging (#9)`).
- Update the active phase's deliverables board status column when a PR merges.

## Where everything lives in code (cheat sheet)

| Concern | File |
| --- | --- |
| Auth0 JWT validation + role policies | `api/QuantamAnalytics.Api/Auth/AuthExtensions.cs` |
| Policy name constants | `api/QuantamAnalytics.Api/Auth/AuthorizationPolicies.cs` |
| Roles + custom-claim namespace | `api/QuantamAnalytics.Domain/Common/Roles.cs` |
| Tenant resolution from JWT | `api/QuantamAnalytics.Api/Tenancy/` |
| Per-request tenant context | `api/QuantamAnalytics.Infrastructure/Tenancy/CurrentTenant.cs` |
| Global query filter wiring | `api/QuantamAnalytics.Infrastructure/Data/AppDbContext.cs` (`ApplyTenantQueryFilters`) |
| R2 / disabled fallback | `api/QuantamAnalytics.Infrastructure/Storage/` |
| Calendar provider abstractions | `api/QuantamAnalytics.Infrastructure/Interviews/` |
| Meeting-link generator (stub) | `api/QuantamAnalytics.Infrastructure/Interviews/MeetingLinkGenerator.cs` |
| Checkr client (stub) | `api/QuantamAnalytics.Infrastructure/BackgroundChecks/StubCheckrClient.cs` |
| DocuSeal client (stub) | `api/QuantamAnalytics.Infrastructure/Esign/StubDocuSealClient.cs` |
| Dice posting client (stub) | `api/QuantamAnalytics.Infrastructure/JobBoards/StubDicePostingClient.cs` |
| Indeed XML feed | `api/QuantamAnalytics.Api/Endpoints/JobFeedEndpoint.cs` (anonymous, per-tenant slug) |
| Client portal endpoints | `api/QuantamAnalytics.Api/Endpoints/ClientPortal{Jobs,Documents}Endpoint.cs` |
| Reporting summary | `api/QuantamAnalytics.Api/Endpoints/ReportingEndpoint.cs` |
| Audit log capture | `api/QuantamAnalytics.Infrastructure/Data/AuditLogSaveChangesInterceptor.cs` (PR-44) |
| Audit log read endpoint | `api/QuantamAnalytics.Api/Endpoints/AuditLogEndpoint.cs` (PR-44, PlatformAdmin only) |
| Anonymous webhook landing pads | `BackgroundCheckEndpoint.cs`, `EsignDocumentEndpoint.cs` (use `IgnoreQueryFilters()` + provider id lookup) |
| Multi-tenant isolation IT fixture | `api/QuantamAnalytics.Tests/Fixtures/{PostgresFixture,IsolatedAppFactory}.cs` (PR-30) |
| Auto-applied migrations on deploy | `.github/workflows/deploy-dev.yml` `apply-migrations` job (PR-31) |
| Verification reference (after deploys) | [`docs/verification.md`](./docs/verification.md) |
| Demo seed data | `api/QuantamAnalytics.Api/Demo/DemoDataSeeder.cs` |
| SPA Auth0 wiring | `client/src/app/app.config.ts` |
| SPA AuthService facade | `client/src/app/core/auth/auth.service.ts` |
| Auth0 tenant setup walkthrough | `docs/auth.md` |
| Azure / OIDC / GitHub setup | `docs/cicd.md` |

## Auth0 / GitHub config (current dev)

| Where | Name | Value |
| --- | --- | --- |
| GitHub *variable* | `AUTH0_DOMAIN` | `quantamanalitics-dev.us.auth0.com` |
| GitHub *variable* | `AUTH0_AUDIENCE` | `https://api.quantamanalitics.com` |
| GitHub *variable* | `AUTH0_CLIENT_ID` | `dpKeOWXoBCCSXNxsHzNnXvsiGgNstWLT` |
| GitHub *secret* | `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` | OIDC federation for Azure deploys |
| GitHub *secret* | `AZURE_STATIC_WEB_APPS_API_TOKEN` | SWA deployment |
| GitHub *secret* | `POSTGRES_CONNECTION_STRING` | Neon pooled, Npgsql keyword form |
| GitHub *secret* | `R2_*` (4 keys) | Cloudflare R2 (only required when uploads are enabled) |

Use **Variables** for public OIDC config (anything embedded in a JWT or HTML bundle is already public). Use **Secrets** for credentials.

## Current state

- **Phases 1 + 2 + 3 + 4: MERGED** (PRs 01–36). Live dev environment serves the full placement workflow, the Indeed XML feed, the Dice posting scaffold, the read-only client portal, the baseline reporting summary, and the ops floor (multi-tenant isolation IT, auto-applied migrations).
- **Phase 5: in progress.** Production readiness, real provider integrations, SaaS productization. PR-44 (SOC 2 audit-log baseline) is in review. The rest of Phase 5 is structured as ten epic-organized stories — see [`plan/phase-5-deliverables.md`](./plan/phase-5-deliverables.md). Each story has explicit prerequisites; most need real partner-API credentials before the PR can merge.

### Phase 4 — what just landed

All eight Phase 4 PRs merged. The platform is demonstrably "client-ready":
recruiters have a baseline reporting dashboard, clients have a read-only
portal over jobs and signed documents, and the dev tenant has a stable
Indeed XML feed URL anyone can hand to Indeed publishers. The two
ops-floor PRs (PR-30 multi-tenant isolation IT, PR-31 auto-applied
migrations) landed first so the rest of Phase 4 could ship safely on top —
and any new Phase 5 endpoint inherits that same safety net for free.

### Phase 5 — how it's structured

Three epics, ten stories, all in [`plan/phase-5-deliverables.md`](./plan/phase-5-deliverables.md):

- **Epic A — Real Provider Integrations** (Stories A1–A5): replace Checkr / DocuSeal / Zoom / Teams stubs with real partner-API calls. A5 (real Dice posting) is gated on a partner contract.
- **Epic B — Production Readiness** (Stories B1–B5): private GHCR + managed-identity image pull, prod Auth0 tenant with MFA, custom domain, audit-log baseline (B4 = PR-44, in review), branch protection.
- **Epic C — SaaS Productization** (Stories C1–C3): self-serve tenant signup, Stripe subscriptions, dunning + write-lock on past-due tenants.

### Standing follow-ups (carry across phases)

- **Webhook signature verification** — captured as part of Stories A1 / A2 / C2. Don't expose the existing stub-era webhook URLs to real providers without HMAC checks.
- **Migrate existing endpoint tests onto the PR-30 PostgresFixture pattern** — `RecruiterInvoiceReadyEndpointTests` and friends still use raw SQL against user-secrets Postgres. Hygiene PR before any new endpoint test lands.
- **Source-of-hire attribution** — `Application` has no source column today; the reporting summary will grow a SourceOfHire section once it does. Separate PR with a migration; not bundled with Phase 5.
- **Document templates in DocuSeal** — Story A2 assumes at least one offer-letter template configured at slug `offer_letter_v1`. Set this up when you sign up for DocuSeal.

## Local dev quick-start

```
# Postgres connection (one-time)
cd api/QuantamAnalytics.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=...;Database=...;Username=...;Password=...;SslMode=Require;Channel Binding=Disable"
dotnet user-secrets set "Auth0:Domain" "quantamanalitics-dev.us.auth0.com"
dotnet user-secrets set "Auth0:Audience" "https://api.quantamanalitics.com"

# Run
cd ../..
cd api && dotnet run --project QuantamAnalytics.Api      # API on :5080
cd client && npm install && npm start                    # SPA on :4200

# Add a migration
cd api
dotnet ef migrations add <Name> \
  --project QuantamAnalytics.Infrastructure \
  --startup-project QuantamAnalytics.Api

# Apply migrations to Neon
dotnet ef database update \
  --project QuantamAnalytics.Infrastructure \
  --startup-project QuantamAnalytics.Api
```

Full Azure / OIDC / Auth0 setup steps live in `docs/cicd.md` and `docs/auth.md`.

## Cost discipline

Build phase target: **$0–10/month total run cost.** If any decision pushes spend above this, raise it explicitly in the PR description with the reason.

## Working with this project (every session)

1. Read this file (you're doing it).
2. Skim `plan/phase-5-deliverables.md` for the next pending story (or earlier phase board if helping with cleanup work).
3. Check the story's **Prerequisites** section — most Phase 5 work is gated on real partner-API setup that has to happen before the code PR opens.
4. Pull latest `main` and branch off (`git checkout main && git pull && git checkout -b qa001-<scope>`).
5. Stay in the slice — no "while I'm here" adjacent work.
6. Update the active phase's deliverables board status column on merge. Keep CLAUDE.md current at every phase boundary.
7. After every dev deploy, walk through the relevant section of [`docs/verification.md`](./docs/verification.md) before claiming a deliverable done.
