# Phase 9 — Intelligence, Integrations & Monetization · 8 Deliverables

**Status: QUEUED.** This phase is where the platform compounds into a harder
to copy SaaS product: API surfaces, automations, stronger scheduling,
monetization add-ons, and AI assistance. The goal is not just polish, but
operational leverage and differentiation.

**Audit coverage:** 3, 4, 7, 18, 25, 26, 37, 39, 40.

| #   | Branch                              | Scope                                                                    | Status |
| --- | ----------------------------------- | ------------------------------------------------------------------------ | ------ |
| 66  | `qa001-phase9-plan`                 | Phase 9 deliverables board + repo handoff after Phase 8                  | queued |
| 67  | `qa001-ai-candidate-matching`       | Resume/JD matching, candidate scorecards, gap explanations               | queued |
| 68  | `qa001-interview-automation`        | Self-booking, interviewer availability, timezone-safe scheduling flows   | queued |
| 69  | `qa001-offer-generator`             | Offer letter generator, compensation breakdown, acceptance tracking      | queued |
| 70  | `qa001-platform-api`                | Customer-facing API surface for jobs, candidates, payroll, submissions   | queued |
| 71  | `qa001-webhook-platform`            | Tenant-safe outbound webhooks, delivery logs, retry policy               | queued |
| 72  | `qa001-billing-experience`          | Billing UI, seat plans, usage limits, trial/upgrade management           | queued |
| 73  | `qa001-ai-copilot-layer`            | AI copilot for summaries, interview questions, checklists, workflow help | queued |

> Order is a guideline. PR-67 (AI matching) and PR-68 (interview automation)
> depend on the real provider and scheduling groundwork from Phase 5, while
> PR-70 / PR-71 should land before any serious third-party ecosystem work.

---

## Definition of Done — Phase 9

Phase 9 ships when all of the following are true:

- Recruiters can see meaningful AI-assisted matching on candidates and jobs
- Interview scheduling behaves like a built-in recruiter tool, not a manual sidecar
- Offer generation and acceptance tracking are native to the product
- Customers can integrate through supported APIs and outbound webhooks
- Billing, seats, and plan management are visible to tenant admins
- AI copilot experiences save real recruiter/admin time without weakening tenant safety

---

## Out of scope for Phase 9

- Full mobile apps
- Enterprise-specific isolation models such as schema-per-tenant or DB-per-tenant
- Bespoke client implementations that do not generalize into the core SaaS roadmap
