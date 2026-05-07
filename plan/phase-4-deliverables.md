# Phase 4 — Distribution & Client Visibility · 8 Deliverables

Phase 4 turns the now-complete placement workflow into something a staffing
firm can use to win and retain client business: outbound job posting, a real
client portal, and the reporting that justifies the platform's value.

Keep the same discipline as Phases 1–3: one branch, one PR, one tightly scoped
deliverable. Branch prefix stays `qa001-`.

| #   | Branch                                 | Scope                                                                  | Status  |
| --- | -------------------------------------- | ---------------------------------------------------------------------- | ------- |
| 29  | `qa001-phase4-plan`                    | Phase 4 deliverables board + repo state handoff from Phase 3           | pending |
| 30  | `qa001-tenant-isolation-it`            | Testcontainers-backed multi-tenant isolation integration test          | pending |
| 31  | `qa001-migrations-in-ci`               | Auto-applied EF migrations on dev deploy + safety guard                | pending |
| 32  | `qa001-job-feed-indeed`                | Indeed XML feed export of public jobs + per-tenant feed URL            | in review |
| 33  | `qa001-job-feed-dice`                  | Dice posting handoff scaffold (interface + stub provider)              | pending |
| 34  | `qa001-client-portal-jobs`             | Client portal: jobs requested, candidates submitted, status visibility | pending |
| 35  | `qa001-client-portal-docs`             | Client-facing document sharing (offers, signed packets) read surface   | pending |
| 36  | `qa001-reporting-baseline`             | Recruiter activity, time-to-fill, source-of-hire baseline reports      | pending |

> Order is a guideline. PR-30 (tenant isolation IT) and PR-31 (migrations in CI)
> should land **before** anything that adds new endpoints or new tables — they
> are the ops floor Phase 4 builds on.

---

## How each deliverable runs

For every PR:

1. **Branch from `main`:** `git checkout main && git pull && git checkout -b qa001-<scope>`
2. **Stay inside the slice** — resist bundling adjacent feed / portal / reporting work
3. **Commit small** with why-focused messages
4. **Open a PR** against `main` using the PR template
5. **Self-review** before merge
6. **Squash-merge** into `main`
7. **Update status** in this file after merge

If any deliverable grows beyond ~600 changed lines, split it before review.

---

## Definition of Done — Phase 4

Phase 4 ships when all of the following are true:

- Multi-tenant isolation is covered by an automated integration test in CI
- EF migrations apply automatically on every dev deploy without a laptop in the loop
- Each tenant has a stable Indeed-compatible job feed URL serving its public jobs
- Dice posting has a real provider abstraction (stub today, partner-API ready)
- Clients can sign in to their portal and see jobs they requested + candidate status
- Clients can read offers and signed onboarding packets through the portal
- A baseline reporting surface exists for recruiter activity and time-to-fill
- README and CLAUDE.md reflect the post-Phase-4 reality

---

## Out of scope for Phase 4 (deferred)

- Full LinkedIn job-board posting (RSC partnership; revenue-gated)
- Custom report builder / saved views (basic fixed reports only in this phase)
- Self-serve tenant signup and Stripe subscription billing — that is Phase 5
- Real Checkr / DocuSeal / Zoom / Teams API integrations — also Phase 5
- SOC 2 Type II artifacts — Phase 5 once a paying customer commits
