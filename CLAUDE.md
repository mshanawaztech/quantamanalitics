# CLAUDE.md — Project Memory

This file is read by Claude on every session in this repo. Treat the rules below as non-negotiable.

## Project

**Quantam Analytics** — a multi-tenant staffing SaaS platform. Built first for one staffing firm, designed from day 1 to be sold as SaaS.

Domain: `quantamanalitics.com`
Owner: Shahnawaz Mohammed (`mshanawaztech@gmail.com`)
GitHub: `mshanawaz114/quantamanalitics`

Full roadmap: [`plan/plan.md`](./plan/plan.md)
Current phase deliverables: [`plan/phase-1-deliverables.md`](./plan/phase-1-deliverables.md)

## Stack (locked — do not change without an ADR)

- Backend: ASP.NET Core 10 LTS, EF Core 10
- Database: PostgreSQL (dev: Neon free tier; prod: Azure Database for PostgreSQL Flexible Server)
- Background jobs: Hangfire (in-process)
- Frontend: Angular (LTS), single SPA with route-based portals (recruiter / client / candidate / contractor)
- Auth: Auth0 free tier (or Azure B2C — picked in PR-06)
- File storage: Cloudflare R2
- Hosting: Azure Container Apps (API), Azure Static Web Apps (web)
- Email: Resend (transactional), Cloudflare Email Routing (mailboxes)
- CI/CD: GitHub Actions
- Infra-as-code: Bicep
- Observability: Application Insights

## Branch & PR rules — non-negotiable

1. **Never push to `main`.** Ever. `main` is protected and only accepts squash-merged PRs.
2. **Branch naming:** `qa001-<short-scope>` (e.g., `qa001-bootstrap`, `qa001-postgres-efcore`). The `qa001` prefix is permanent.
3. **One PR = one deliverable.** If a PR exceeds ~600 changed lines, split it.
4. **PR template required** — see `.github/pull_request_template.md`. Fill in every section.
5. **Squash-merge only.** Keeps `main` history linear and readable.
6. **Commit message style:** `<type>: <short summary>` where type is one of `feat | fix | chore | docs | refactor | test | ci`. Body explains *why*, not *what*.
7. **Update CLAUDE.md and README.md at every milestone PR** (currently at the end of each Phase). Stale docs are worse than no docs.

## Folder structure (forecasted — populated as PRs land)

```
quantamanalitics/
├── api/                       ← .NET 10 solution (PR-02)
│   ├── QuantamAnalytics.Api/
│   ├── QuantamAnalytics.Domain/
│   ├── QuantamAnalytics.Infrastructure/
│   └── QuantamAnalytics.Tests/
├── client/                    ← Angular workspace (PR-02)
├── infra/                     ← Bicep (PR-04)
├── plan/                      ← roadmap docs
│   ├── plan.md
│   └── phase-1-deliverables.md
├── docs/                      ← ADRs, runbooks (added as needed)
│   └── adr/
├── .github/
│   ├── workflows/
│   └── pull_request_template.md
├── .claude/                   ← Claude project settings + skills
│   ├── settings.json
│   └── skills/
├── CLAUDE.md                  ← this file
├── README.md
├── CONTRIBUTING.md
├── LICENSE
├── .gitignore
└── .editorconfig
```

## Things to NEVER do

- Never commit secrets (API keys, connection strings, tokens). Use Azure Key Vault. `.env` files are gitignored — keep them that way.
- Never push directly to `main`. Always PR.
- Never disable EF Core global query filters in production code. Tenant isolation depends on them.
- Never store sensitive PII (SSN, DOB) without encryption-at-rest column types.
- Never accept a tenant_id from the client — always derive from the authenticated user's JWT.
- Never bump a major dependency version without an ADR explaining why.
- Never skip the PR template.

## Things to ALWAYS do

- Always run `dotnet format` and `npm run lint` before committing
- Always include the `qa001-` prefix on branch names
- Always reference the PR number in commit messages once a PR is open (`feat(jobs): list endpoint paging (#9)`)
- Always update `plan/phase-1-deliverables.md` status column when a PR merges

## Current state

- **Active phase:** Phase 1 — MVP
- **Active deliverable:** PR-01 · Bootstrap (in progress on `qa001-bootstrap`)
- **Next deliverable:** PR-02 · Solution scaffold (.NET 10 + Angular)

## Cost discipline

Build phase target: **$0–10/month total run cost.** If any decision pushes spend above this, raise it explicitly in the PR description with the reason. The user is bootstrapping — every recurring dollar matters.

## Working with this project

Before starting any work session in this repo:

1. Read this file (you're doing it).
2. Read `plan/plan.md` if context is light.
3. Check `plan/phase-1-deliverables.md` for the next pending deliverable.
4. Confirm you're on a `qa001-*` branch before making changes.
5. Create the branch from latest `main` if not.
