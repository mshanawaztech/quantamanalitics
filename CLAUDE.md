# CLAUDE.md — Project Memory

This file is read by Claude on every session in this repo. Treat the rules below as non-negotiable.

## Project

**Quantam Analytics** — a multi-tenant staffing SaaS platform. Built first for one staffing firm, designed from day 1 to be sold as SaaS.

- Domain: `quantamanalitics.com`
- Owner: Shahnawaz Mohammed (`mshanawaztech@gmail.com`)
- GitHub: `mshanawaz114/quantamanalitics`
- Live dev SPA: `https://ambitious-dune-099500b0f.7.azurestaticapps.net`
- Live dev API: `https://ca-qa-dev-api.icymushroom-94be4003.eastus2.azurecontainerapps.io`

Roadmap: [`plan/plan.md`](./plan/plan.md). Per-phase boards: [`plan/phase-1-deliverables.md`](./plan/phase-1-deliverables.md), [`plan/phase-2-deliverables.md`](./plan/phase-2-deliverables.md), [`plan/phase-3-deliverables.md`](./plan/phase-3-deliverables.md).

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
│   │                       Submission, InterviewEvent, Timesheet,
│   │                       TimeEntry, TimesheetTotals
│   ├── QuantamAnalytics.Infrastructure/
│   │   ├── Data/        ← AppDbContext + per-entity Configurations/
│   │   ├── Tenancy/     ← CurrentTenant, ICurrentTenant{,Setter}
│   │   ├── Storage/     ← R2 + DisabledResumeStorage fallback
│   │   ├── Interviews/  ← Calendar provider + meeting link abstractions
│   │   └── Migrations/
│   ├── QuantamAnalytics.Api/
│   │   ├── Auth/        ← AuthExtensions, AuthorizationPolicies, StartupLog
│   │   ├── Tenancy/     ← Tenant resolver middleware
│   │   ├── Demo/        ← DemoDataSeeder (startup seed for repeatable previews)
│   │   └── Endpoints/   ← Health, Me, PublicJobs, RecruiterPortal,
│   │                       CandidateProfile, ContractorTimesheet,
│   │                       ClientApproval, InterviewScheduling
│   └── QuantamAnalytics.Tests/
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

- **Phases 1 + 2: MERGED** (PRs 01–20). Live dev environment serves the marketing site, public jobs board, candidate intake, recruiter portal, contractor timesheets, client approval, pay rules, invoice staging, QBO/Stripe scaffold.
- **Phase 3: in flight.** PR-21 (plan), PR-22 (submissions), PR-23 (interview scheduling shell), PR-24 (calendar baseline) merged. Up next: PR-25 → 28.

### Phase 3 — what's still pending

| # | Branch | Scope |
| --- | --- | --- |
| 25 | `qa001-meeting-link-scaffold` | Zoom/Teams meeting-link generation on interview events |
| 26 | `qa001-checkr-baseline` | Checkr request/status baseline + webhook-ready persistence |
| 27 | `qa001-esign-docuseal` | Offer-letter / onboarding packet handoff via DocuSeal |
| 28 | `qa001-onboarding-forms` | Candidate onboarding forms collection + document checklist |

Pick the next branch off latest `main`. Update `plan/phase-3-deliverables.md` status column on merge.

### Standing follow-ups (carry across phases)

- **Multi-tenant isolation integration test** — Testcontainers-backed: two synthetic JWTs with different tenant claims, assert tenant A's request can't read tenant B's row. Highest-value test in the codebase; not yet written.
- **Auto-applied migrations in CI** — currently manual via `dotnet ef database update` from a laptop. Phase 4 ops cleanup.
- **Branch protection on `main`** — confirm at `Settings → Branches` that the four CI checks are required.
- **Phase 4+ topics** — production hardening (private GHCR + managed identity pull), prod tenant in Auth0, MFA + breach-password detection, custom domain.

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
2. Skim `plan/phase-3-deliverables.md` for what's pending.
3. Pull latest `main` and branch off (`git checkout main && git pull && git checkout -b qa001-<scope>`).
4. Stay in the slice — no "while I'm here" adjacent work.
5. Update the deliverables board status column on merge. Keep CLAUDE.md current at every phase boundary.
