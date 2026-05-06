# Quantam Analytics

A multi-tenant staffing platform — applicant tracking, interview management, onboarding, timesheets, invoicing, and outbound job-board posting. Built first for one staffing firm, designed to be sold as SaaS.

**Status:** Phase 2 is merged and live in the shared dev environment. Phase 3 planning is queued in [`plan/phase-3-deliverables.md`](./plan/phase-3-deliverables.md). See [`plan/plan.md`](./plan/plan.md) for the full roadmap.

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
- startup-seeded demo tenants/jobs/applications for repeatable previews
- SPA `404` handling and client-routed deep-link deploy smoke checks

This is still a dev preview, not a production-ready operations closeout:

- candidate resume upload still depends on the environment R2 secrets being present
- QuickBooks and Stripe are baseline handoff surfaces, not live external integrations yet
- dev migrations are still applied manually outside the deploy workflow

See [`plan/phase-1-deliverables.md`](./plan/phase-1-deliverables.md) for the completed MVP sequence, [`plan/phase-2-deliverables.md`](./plan/phase-2-deliverables.md) for the completed Time & Money phase, [`plan/phase-3-deliverables.md`](./plan/phase-3-deliverables.md) for the active next queue, and [`plan/plan.md`](./plan/plan.md) for later phases.

## License

MIT — see [`LICENSE`](./LICENSE).
