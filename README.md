# Quantam Analytics

A multi-tenant staffing platform — applicant tracking, interview management, onboarding, timesheets, invoicing, and outbound job-board posting. Built first for one staffing firm, designed to be sold as SaaS.

**Status:** Phase 1 (MVP) in progress. See [`plan/plan.md`](./plan/plan.md) for the full roadmap.

## Stack

ASP.NET Core 10 · EF Core 10 · PostgreSQL · Angular (LTS) · Azure Container Apps · Azure Static Web Apps · Cloudflare R2 · Auth0 · Hangfire · Bicep · GitHub Actions

## Repository layout

```
api/         ASP.NET Core 10 solution (Domain, Infrastructure, Api, Tests)
client/      Angular workspace
infra/       Bicep templates for Azure resources
plan/        Multi-phase roadmap and per-phase deliverable lists
.claude/     Project-level Claude skills and settings
.github/     PR template and CI/CD workflows
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

## Contributing

This is a single-maintainer project, but the workflow is enforced strictly to keep `main` healthy and to keep AI-assisted contributions reviewable.

- All work happens on `qa001-<scope>` branches
- All changes go through a PR — no direct pushes to `main`
- See [`CONTRIBUTING.md`](./CONTRIBUTING.md) for branch naming, PR checklist, and commit conventions
- See [`CLAUDE.md`](./CLAUDE.md) if you're using Claude / Cowork to contribute

## Phase 1 — MVP scope

A guest visitor browses jobs and applies; a candidate signs up, uploads a resume, and tracks applications; a recruiter logs in, posts jobs, and moves applications across a Kanban pipeline. Two seeded tenants prove multi-tenant isolation. Auto-deployed to a dev environment from `main`.

Twelve small PRs to get there — see [`plan/phase-1-deliverables.md`](./plan/phase-1-deliverables.md).

## License

MIT — see [`LICENSE`](./LICENSE).
