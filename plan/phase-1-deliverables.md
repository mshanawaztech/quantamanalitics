# Phase 1 — MVP · 12 Deliverables

Each row below is one branch + one PR + one squash-merge. Small, reviewable, cheap on context budget. Do them in order — each builds on the previous.

| #   | Branch                          | Scope                                                                          | Status |
| --- | ------------------------------- | ------------------------------------------------------------------------------ | ------ |
| 01  | `qa001-bootstrap`               | README, CLAUDE.md, plan, conventions, gitignore, PR template, .claude/ folder  | merged |
| 02  | `qa001-solution-scaffold`       | ASP.NET Core 10 solution + Angular workspace + `/health` endpoint              | merged |
| 03  | `qa001-postgres-efcore`         | EF Core 10 + Npgsql, initial migration with `Tenants`, Neon dev connection     | in review |
| 04  | `qa001-infra-bicep-dev`         | Bicep for `rg-quantamanalitics-dev`: Container Apps, SWA, Key Vault, App Insights | pending |
| 05  | `qa001-cicd`                    | `.github/workflows/ci.yml` + `deploy-dev.yml`                                  | pending |
| 06  | `qa001-auth`                    | Auth0 integration, JWT validation, Angular guards, role enum                   | pending |
| 07  | `qa001-multi-tenant`            | `TenantId` everywhere, EF global query filter, tenant resolver middleware      | pending |
| 08  | `qa001-marketing-site`          | Angular public routes: Home, About, Services, Contact + responsive layout      | pending |
| 09  | `qa001-public-job-board`        | `GET /api/v1/jobs`, `/jobs` and `/jobs/:id` Angular pages, guest apply form    | pending |
| 10  | `qa001-candidate-profile`       | Signup, resume upload to R2, profile fields, candidate dashboard skeleton      | pending |
| 11  | `qa001-recruiter-portal`        | Recruiter login, jobs CRUD, applications Kanban (Applied → Hired/Rejected)     | pending |
| 12  | `qa001-phase1-hardening`        | 404/500 pages, loading states, smoke tests, demo seeder, milestone doc updates | pending |

---

## How each deliverable runs

For every PR:

1. **Branch from `main`:** `git checkout main && git pull && git checkout -b qa001-<scope>`
2. **Build only what's in scope** — don't pull in adjacent work even if it feels related
3. **Commit small, commit often** with descriptive messages
4. **Open a PR** against `main` using the PR template
5. **Self-review** — read the diff before merging
6. **Squash-merge** into `main`
7. **Update task status** in CLAUDE.md "current deliverable" pointer

If a deliverable starts ballooning past ~600 changed lines, **split it into 2 PRs** rather than push through. Small PRs ship; big PRs rot.

---

## Definition of Done — Phase 1

Phase 1 ships when all of the following are true:

- A guest visitor can land on the marketing site, browse jobs, view a job detail, and submit an application
- A candidate can sign up, upload a resume, and see their applied jobs
- A recruiter can log in, create a job, see applications come in, and move them across the Kanban
- The same flow works for two seeded tenants without data leaking between them
- Everything is auto-deployed to `dev.quantamanalitics.com` from `main`
- Total monthly run cost is verified at $0–10
- README and CLAUDE.md reflect Phase 1 reality

---

## Out of scope for Phase 1 (deferred)

Anything in this list belongs to later phases — saying "no" here keeps Phase 1 shippable:

- Timesheets, invoicing, BGC, e-sign, interview scheduling — Phase 2 / 3
- Outbound posting to Indeed / Dice / LinkedIn — Phase 4
- Client portal — Phase 4
- White-label theming, tenant self-signup, Stripe billing — Phase 5
- Mobile apps (iOS / Android) — out of roadmap entirely; PWA is enough
