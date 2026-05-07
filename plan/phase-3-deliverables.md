# Phase 3 — Hiring Workflow Depth · 8 Deliverables

Phase 3 extends the live recruiting, time, and billing baseline into a fuller
placement workflow: interview coordination, background checks, e-sign, and
onboarding forms. Keep the same discipline: one branch, one PR, one tightly
scoped deliverable.

| #   | Branch                               | Scope                                                                  | Status  |
| --- | ------------------------------------ | ---------------------------------------------------------------------- | ------- |
| 21  | `qa001-phase3-plan`                  | Phase 3 deliverables board + repo state handoff from Phase 2           | pending |
| 22  | `qa001-submission-domain`            | Submission aggregate + recruiter/client handoff state model            | pending |
| 23  | `qa001-interview-scheduling-shell`   | Interview route shell, scorecard model, and scheduler status surfaces  | pending |
| 24  | `qa001-google-outlook-baseline`      | Calendar provider baseline and interview event handoff abstraction     | pending |
| 25  | `qa001-meeting-link-scaffold`        | Zoom/Teams meeting link generation scaffold on interview events        | pending |
| 26  | `qa001-checkr-baseline`              | Checkr request/status baseline with webhook-ready persistence          | in review |
| 27  | `qa001-esign-docuseal`               | Offer-letter and onboarding packet handoff scaffold via DocuSeal       | in review |
| 28  | `qa001-onboarding-forms`             | Candidate onboarding forms collection status and document checklist    | pending |

---

## How each deliverable runs

For every PR:

1. **Branch from `main`:** `git checkout main && git pull && git checkout -b qa001-<scope>`
2. **Stay inside the slice** — resist bundling adjacent interview / compliance / onboarding work
3. **Commit small** with why-focused messages
4. **Open a PR** against `main` using the PR template
5. **Self-review** before merge
6. **Squash-merge** into `main`
7. **Update status** in this file after merge

If any deliverable grows beyond ~600 changed lines, split it before review.

---

## Definition of Done — Phase 3

Phase 3 ships when all of the following are true:

- Recruiters can move approved candidates into a deeper submission/interview workflow
- Interview scheduling has a real provider abstraction and persistent event model
- Meeting creation has a working baseline for calendar-linked calls
- Background-check requests and status updates are represented end to end
- Offer/onboarding document handoff has a real e-sign baseline
- Candidate onboarding forms have a visible portal status and persistence model
- README and AGENTS.md reflect the post-Phase-3 reality

---

## Out of scope for Phase 3 (deferred)

- Full payroll and paystub generation
- Production-grade partner sync conflict handling and retries
- Deep client portal expansion beyond hiring visibility
- Full SaaS self-serve productization and tenant billing
