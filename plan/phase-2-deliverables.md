# Phase 2 — Time & Money · 8 Deliverables

Phase 2 extends the recruiting MVP into contractor time capture, client approval,
and invoice-ready operations. Keep the same Phase 1 discipline: one branch, one
PR, one narrowly-scoped deliverable.

| #   | Branch                               | Scope                                                                 | Status  |
| --- | ------------------------------------ | --------------------------------------------------------------------- | ------- |
| 13  | `qa001-phase2-plan`                  | Phase 2 deliverables board + repo state handoff from Phase 1          | merged  |
| 14  | `qa001-timesheet-domain`             | `Timesheet`, `TimeEntry`, and status model + EF migration             | merged  |
| 15  | `qa001-contractor-portal-shell`      | Protected contractor routes, nav shell, and portal status card        | merged  |
| 16  | `qa001-timesheet-entry`              | Weekly contractor time entry API + Angular draft/submit workflow      | merged  |
| 17  | `qa001-client-approval`              | Client approval/reject flow for submitted timesheets                  | merged  |
| 18  | `qa001-pay-rules`                    | PTO + overtime calculation scaffolding on approved time               | merged  |
| 19  | `qa001-invoice-staging`              | Invoice-ready aggregates and recruiter/admin review surface           | merged  |
| 20  | `qa001-qbo-stripe-baseline`          | QuickBooks export baseline + Stripe fallback invoice handoff scaffold | merged  |

---

## How each deliverable runs

For every PR:

1. **Branch from `main`:** `git checkout main && git pull && git checkout -b qa001-<scope>`
2. **Stay inside the slice** — resist bundling adjacent contractor / approval / billing work
3. **Commit small** with why-focused messages
4. **Open a PR** against `main` using the PR template
5. **Self-review** before merge
6. **Squash-merge** into `main`
7. **Update status** in this file after merge

If any deliverable grows beyond ~600 changed lines, split it before review.

---

## Definition of Done — Phase 2

Phase 2 ships when all of the following are true:

- A contractor can sign in, fill a weekly timesheet, save drafts, and submit it
- A staffing/client-side approver can approve or reject submitted time
- Approved time can be turned into invoice-ready totals without cross-tenant leakage
- PTO and overtime rules are represented in the model and reflected in totals
- QuickBooks/Stripe handoff has a real integration baseline instead of placeholder copy
- README and AGENTS.md reflect the post-Phase-2 reality

---

## Out of scope for Phase 2 (deferred)

- Full paystub generation and payroll engine logic
- Production-grade QuickBooks sync conflict handling
- Complex state labor compliance rules beyond baseline overtime/PTO support
- Client portal expansion beyond time approval
- Phase 3 workflow depth (BGC, interview scheduling, e-sign, onboarding forms)
