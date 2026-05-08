# Quantam Analytics

A multi-tenant staffing platform — applicant tracking, interview management, onboarding, timesheets, invoicing, and outbound job-board posting. Built first for one staffing firm, designed to be sold as SaaS.

**Status:** Phases 1 + 2 + 3 + 4 are merged and live in the shared dev
environment. Phase 5 is in progress (plan handoff + SOC 2 baseline merged;
real provider integrations, prod hardening, and self-serve tenant work still
open). Phase 6 product polish is also merged and live. The active next queue is
the remaining Phase 5 backlog in [`plan/phase-5-deliverables.md`](./plan/phase-5-deliverables.md).
See [`plan/plan.md`](./plan/plan.md) for the full roadmap.

## Stack

ASP.NET Core 10 · EF Core 10 · PostgreSQL · Angular (LTS) · Azure Container Apps · Azure Static Web Apps · Cloudflare R2 · Auth0 · Hangfire · Bicep · GitHub Actions

## Repository layout

```
api/         ASP.NET Core 10 solution (Domain, Infrastructure, Api, Tests) + Dockerfile
client/      Angular workspace
infra/       Bicep templates for Azure resources
plan/        Multi-phase roadmap and per-phase deliverable lists
docs/        Setup guides and ADRs
.claude/     Project-level Claude skills and settings
.github/     PR template and CI/CD workflows (ci.yml, deploy-dev.yml)
```

## Local development

### Prerequisites

- .NET 10 SDK (`dotnet --version` should report `10.0.x`)
- Node.js LTS (for the Angular client)
- A free [Neon](https://neon.tech) Postgres project (used as the dev database)
- EF Core CLI tools — install once: `dotnet tool install --global dotnet-ef`

### One-time database setup

1. Create a free Neon project at [console.neon.tech](https://console.neon.tech). Pick the region nearest you.
2. From the Neon dashboard, copy the **connection string** (the pooled one, with `-pooler` in the host). It looks like:
   ```
   Host=ep-xxx-pooler.us-east-2.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=...;SslMode=Require
   ```
3. Store it locally with `dotnet user-secrets` (never commit it):
   ```bash
   cd api/QuantamAnalytics.Api
   dotnet user-secrets set "ConnectionStrings:Postgres" "Host=...;Database=...;Username=...;Password=...;SslMode=Require"
   ```
4. Apply the schema migration to your Neon DB:
   ```bash
   cd api
   dotnet ef database update \
     --project QuantamAnalytics.Infrastructure \
     --startup-project QuantamAnalytics.Api
   ```

### Run the stack

```bash
# API on http://localhost:5080
cd api && dotnet run --project QuantamAnalytics.Api

# Client on http://localhost:4200
cd client && npm install && npm start
```

The Angular landing card probes `GET /health` (liveness, no DB) and the API also exposes `GET /ready` (readiness, pings Postgres). Use `/ready` to confirm your connection string is good before assuming anything else is broken.

### Running tests

```bash
cd api && dotnet test
```

### Adding a new EF Core migration

```bash
cd api
dotnet ef migrations add <DescriptiveName> \
  --project QuantamAnalytics.Infrastructure \
  --startup-project QuantamAnalytics.Api
```

## CI/CD

Every PR runs `.github/workflows/ci.yml` (build + test + lint for the API, the client, the Bicep, and the Docker image). Branch protection on `main` requires it to pass.

Every squash-merge into `main` runs `.github/workflows/deploy-dev.yml` (build → push to GHCR → re-deploy Bicep with the new image → smoke-test `/health` → build Angular bundle → ship to SWA → probe live SPA routes). Authenticates to Azure via OIDC federation — no client secret stored anywhere.

One-time setup (Azure SP, federated credentials, GitHub secrets, branch protection) is documented in [`docs/cicd.md`](./docs/cicd.md).

## Contributing

This is a single-maintainer project, but the workflow is enforced strictly to keep `main` healthy and to keep AI-assisted contributions reviewable.

- All work happens on `qa001-<scope>` branches
- All changes go through a PR — no direct pushes to `main`
- See [`CONTRIBUTING.md`](./CONTRIBUTING.md) for branch naming, PR checklist, and commit conventions
- See [`CLAUDE.md`](./CLAUDE.md) if you're using Claude / Cowork to contribute

## Current dev preview

The shared dev environment now demonstrates:

- public marketing pages plus a live public jobs board
- guest application intake from `/jobs/:slug`
- Auth0 login with `/me` verification and tenant-aware JWT claims
- candidate profile + resume metadata storage
- candidate applied jobs history after sign-in
- recruiter jobs CRUD and application stage movement
- contractor weekly timesheet entry, client approval, pay rules, invoice staging
- QuickBooks Online + Stripe baseline handoff surfaces
- recruiter submission state machine + interview scheduling shell
- Google / Outlook calendar provider abstraction with Zoom / Teams meeting-link generation (deterministic stub)
- Checkr background-check request / status surface with anonymous webhook landing pad
- DocuSeal offer-letter and onboarding-packet handoff surface with anonymous webhook landing pad
- Candidate onboarding forms checklist with recruiter approve / reject review surface
- per-tenant Indeed XML feed at `/api/v1/feeds/{tenantSlug}/indeed.xml`
- Dice posting handoff scaffold for recruiter-driven outbound posting
- read-only client portal: jobs at the tenant, candidates submitted, signed documents
- baseline reporting summary: application funnel, time-to-fill, recruiter activity
- Testcontainers-backed multi-tenant isolation integration test
- auto-applied EF migrations on every dev deploy (no more laptop-bound `dotnet ef database update`)
- startup-seeded demo tenants / jobs / applications for repeatable previews
- SPA `404` handling and client-routed deep-link deploy smoke checks
- shared design tokens, style guide, and global shell across the SPA
- redesigned recruiter / client / candidate / contractor portals with live seeded demo data
- contractor invoice submission, working details, and client-side invoice approval surfaces
- accessibility statement page plus opt-in CI accessibility checks

This is still a dev preview, not a production-ready operations closeout:

- candidate resume upload still depends on the environment R2 secrets being present
- QuickBooks, Stripe, Checkr, DocuSeal, Zoom, Teams, and Dice are still baseline handoff surfaces today, not live external integrations
- production hardening (private GHCR + managed-identity pull, prod Auth0 tenant, custom domain) remains open in Phase 5
- self-serve tenant signup and Stripe subscription billing remain open in Phase 5

See [`plan/phase-1-deliverables.md`](./plan/phase-1-deliverables.md) for the completed MVP sequence, [`plan/phase-2-deliverables.md`](./plan/phase-2-deliverables.md) for the completed Time & Money phase, [`plan/phase-3-deliverables.md`](./plan/phase-3-deliverables.md) for the completed Hiring Workflow Depth phase, [`plan/phase-4-deliverables.md`](./plan/phase-4-deliverables.md) for the completed Distribution & Client Visibility phase, [`plan/phase-5-deliverables.md`](./plan/phase-5-deliverables.md) for the active remaining SaaS/integration queue, [`plan/phase-6-deliverables.md`](./plan/phase-6-deliverables.md) for the completed UX MVP catch-up, and [`plan/plan.md`](./plan/plan.md) for the full roadmap.

## License

MIT — see [`LICENSE`](./LICENSE).
