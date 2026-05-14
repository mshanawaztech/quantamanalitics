# Phase 7 — Recruiting Operations Maturity · 8 Deliverables

**Status: IN PROGRESS.** This phase turns the current portals from a strong demo into
an everyday recruiter operating surface. It is derived from the SaaS product
audit and focuses on the highest-velocity workflow improvements first:
candidate visibility, collaboration, search, pipeline management, and clearer
portal UX. It intentionally assumes the open Phase 5 real integrations land
before or alongside the pieces that depend on them.

**Audit coverage:** 1, 2, 5, 6, 12, 13, 14, 15, 16, 17, 19, 20, 28, 29, 30, 31,
32.

| #   | Branch                              | Scope                                                                    | Status |
| --- | ----------------------------------- | ------------------------------------------------------------------------ | ------ |
| 50  | `qa001-phase7-plan`                 | Phase 7 deliverables board + repo handoff after Phase 6                  | merged |
| 51  | `qa001-candidate-timeline`          | Candidate activity feed, timeline events, comments, mentions, audit join | merged |
| 52  | `qa001-pipeline-dnd`                | Drag-and-drop recruiter pipeline, bulk moves, stuck-stage SLA surfacing  | merged |
| 53  | `qa001-resume-parsing`              | Resume upload parsing into candidate profile fields + recruiter review    | merged |
| 54  | `qa001-email-templates`             | Reusable template system for invites, rejects, onboarding, offers        | merged |
| 55  | `qa001-search-tags-bulk`            | Advanced search, saved filters, candidate tags, and bulk actions         | merged |
| 56  | `qa001-candidate-portal-v2`         | Candidate-side timeline, application tracking, docs/tasks progress        | merged |
| 57  | `qa001-notifications-analytics`     | Notification center, recruiter dashboards, loading/empty/error polish    | open   |

> Order is a guideline. PR-51 (timeline) and PR-53 (resume parsing) should land
> before PR-56, because the upgraded candidate portal needs real underlying
> activity and profile data to be worth shipping.

---

## Definition of Done — Phase 7

Phase 7 ships when all of the following are true:

- Recruiters can understand a candidate's history from one timeline surface
- Pipeline movement supports drag-and-drop plus bulk movement without refresh
- Resume upload can populate candidate profile fields instead of forcing manual entry
- Recruiters can reuse email templates with merge fields across common workflows
- Search works across tags, skills, stages, locations, and saved filter presets
- Candidates can see meaningful application progress and next-step tasks in their portal
- The product has centralized notifications, real KPI surfaces, and polished loading/empty/error states

---

## Out of scope for Phase 7

- Real Checkr / DocuSeal / Zoom / Teams partner integrations — tracked in Phase 5
- Subscription billing, self-serve tenant signup, and white-labeling — tracked in Phase 5
- Public API platform, customer webhooks, and AI matching/copilot — tracked in later phases
