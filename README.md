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

Each component will document its own `Local development` section as it lands. As of the bootstrap PR, the repo contains only planning and governance — no runnable code yet. The next deliverable (PR-02) scaffolds the .NET solution and Angular workspace.

Once PR-02 lands, local dev will be:

```bash
# API
cd api && dotnet run --project QuantamAnalytics.Api

# Client
cd client && npm install && npm start
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
