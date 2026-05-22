# Quantam Analytics — Product Roadmap

**Positioning:** AI Operations OS for consulting & service companies — the unified
onboarding → staffing → delivery → time → invoicing → offboarding lifecycle, with
AI copilots assisting (never silently acting) along the way.

This roadmap maps the 10 enhancement epics against what already exists in the
codebase, names the real gap, and sequences the work into shippable waves. It is
the source of truth we update as we deliver.

---

## Architectural decisions

**AI provider seam.** AI features sit behind interfaces (`IResumeParser`,
`ICandidateMatcher`, copilot provider). We add a real **Claude (Anthropic)**
implementation selected by configuration:

- No `Anthropic:ApiKey` configured → deterministic **stub** stays the default.
  CI, local dev, and tests are unaffected.
- `Anthropic:ApiKey` present → real LLM activates for summaries, parsing,
  recommendations, and copilot answers.

This lets us build every "AI" story now and switch it on later with a secret.

**AI safety.** Per platform policy, AI **assists** — it drafts, summarizes, and
recommends. Any state-changing action (approve, pay, offboard, revoke access)
stays behind an explicit human action and the existing audit log.

**Delivery shape.** Thin vertical slices (one coherent backend + UI + tests per
PR), branched off `main`, merged when CI is green. No big-bang branches.

---

## Current-state inventory (what already exists)

| Epic | Existing building blocks | Real gap |
|------|--------------------------|----------|
| 1 Onboarding | `OnboardingChecklist` (assign/approve/submit), `ResumeParse` (stub), `CandidateProfile`, copilot checklist gen | Resume→profile auto-create; workflow-by-type; HR doc expiry; status dashboard; real AI parse |
| 2 Allocation / bench | `CandidateMatch` + `ICandidateMatcher` (stub), `RecruiterPortal` submissions | Skill/availability search; bench + utilization scores; demand forecast; AI staffing recs |
| 3 Timesheets | `ContractorTimesheet`, `ClientApproval` | Screenshot/notes → timesheet (AI); duplicate/anomaly detection; project burn rate |
| 4 Invoices | `Invoice`, `Billing`, QuickBooks CSV, approve/reject/mark-paid UI (shipped) | Invoice-from-approved-timesheet; budget validation; approval chains; vendor payment-status portal |
| 5 Copilot | `Copilot` endpoint + `StubCopilotProvider` (summary, interview Qs, checklist) | Real LLM; NL ops Q&A; project risk alerts; configurable approval thresholds |
| 6 Client workspace | `ClientPortalJobs`, `ClientPortalDocuments`, `ClientApproval` | Engagement workspaces; deliverable uploads; milestone approval; SLA + profitability |
| 7 Offboarding | `Esign`, `AuditLog` (adjacent) | Offboarding workflow; access revocation hooks; knowledge capture; task reassignment; audit |
| 8 Compliance | `EsignDocument` (DocuSeal), `BackgroundCheck` (Checkr), `AuditLog` | Cert/contract expiry tracking; doc approval workflow; e-sign surfacing; readiness reports |
| 9 Forecasting | `Reporting` summary | Revenue-from-allocation forecast; utilization trends; project profitability; leakage detection |
| 10 Marketplace | `Webhook`, `PlatformApi`, `DicePosting`, Indeed `JobFeed` | CRM/HR/payroll connectors; Slack/Teams triggers; reusable templates; automation packs |

---

## Delivery waves (sequenced for fastest PMF)

### Wave 1 — Close the core lifecycle loops (highest leverage, mostly wiring)
1. **E4.1 Invoice from approved timesheet** — one click turns approved time into a draft invoice. (timesheets + invoices + approval already exist)
2. **E1.1 Resume → consultant profile** — upload a resume, auto-create a profile from parsed fields.
3. **E1.5 Onboarding status dashboard** — completion % per consultant from existing checklist data.
4. **E3.5 Project burn rate** — real-time hours/$ burn from timesheets, surfaced in reporting.

### Wave 2 — AI activation (flip stubs to real Claude behind config)
5. **E5.1 NL ops Q&A copilot** — ask operational questions over tenant data.
6. **E1.1+ Real AI resume parse** — replace `StubResumeParser` with Claude impl.
7. **E2.4 AI staffing recommendations** — real `ICandidateMatcher` over skills/availability.
8. **E5.2 Project risk alerts** — AI flags stuck submissions / burn anomalies.

### Wave 3 — Resource & utilization intelligence
9. **E2.1 Consultant search** (skill/availability/cert) · **E2.3 bench + utilization**.
10. **E9.2 Utilization trends** · **E9.1 revenue-from-allocation forecast** · **E9.3 project profitability**.
11. **E3.4 Timesheet anomaly/duplicate detection**.

### Wave 4 — Client engagement & compliance
12. **E6.1 Engagement workspaces** · **E6.3 milestone approval** · **E6.4 SLA/health**.
13. **E8.1 Cert/contract expiry tracking** · **E8.5 compliance readiness report** · **E8.4 surface e-sign**.

### Wave 5 — Offboarding & lifecycle closure
14. **E7.1 Offboarding workflow** · **E7.2 access-revocation hooks** · **E7.4 task reassignment** · **E7.5 audit**.

### Wave 6 — Extensibility (stickiness)
15. **E10.2 Slack/Teams triggers** · **E10.3 reusable workflow templates** · **E10.1 connectors** · **E10.5 automation packs**.

---

## Definition of done (per slice)
- Backend endpoint(s) + domain logic with unit/integration tests.
- UI surfaced in the relevant portal, matching existing design tokens.
- AI paths behind the provider seam (stub default), human-in-the-loop for actions.
- CI green; merged via PR; roadmap row updated.

---

## Status log
- _2026-05-22_ — Roadmap created. Prereqs already shipped on `qa028-invoice-fixes`:
  invoice save fix, tenant validation, preview, **client invoice approval UI**,
  branding PII scrub.
