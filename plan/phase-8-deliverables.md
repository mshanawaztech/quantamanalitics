# Phase 8 — Enterprise Control Plane · 8 Deliverables

**Status: IN PROGRESS.** This phase takes the platform from "strong operations app"
to "credible enterprise system." It focuses on governance, security,
recoverability, and tenant trust. Several of these items already have baseline
foundation in the codebase; this phase is where they become first-class
customer-facing capabilities.

**Audit coverage:** 9, 10, 11, 21, 22, 23, 24, 33, 34, 35, 36, 38.

| #   | Branch                              | Scope                                                                      | Status |
| --- | ----------------------------------- | -------------------------------------------------------------------------- | ------ |
| 58  | `qa001-phase8-plan`                 | Phase 8 deliverables board + repo handoff after Phase 7                    | merged |
| 59  | `qa001-rbac-granular`               | Granular role/permission matrix for recruiter, HR, payroll, interviewer    | merged |
| 60  | `qa001-audit-console`               | Queryable audit log UI, subject/history views, login/access review surfaces | open   |
| 61  | `qa001-soft-delete-recovery`        | Soft delete, recycle bin, restore windows, retention rules                 | queued |
| 62  | `qa001-file-security`               | Signed URLs, expiring downloads, encrypted resume/document handling         | queued |
| 63  | `qa001-auth-hardening`              | MFA, session management, suspicious login handling, stronger password flows | queued |
| 64  | `qa001-trust-center`                | Privacy, terms, cookie, GDPR, security pages and tenant trust center       | queued |
| 65  | `qa001-ops-observability`           | Monitoring, backup/restore runbooks, disaster recovery, feature flags       | queued |

> Order matters. PR-59 (granular RBAC) and PR-60 (audit console) should land
> before broader rollout of recoverability and trust-center features, because
> most enterprise buyers will evaluate permissioning and accountability first.

---

## Definition of Done — Phase 8

Phase 8 ships when all of the following are true:

- Permissioning is granular enough to separate recruiter, HR, payroll, interviewer, and manager responsibilities
- Audit history is readable and reviewable from the product, not only the database
- Deleted records can be recovered safely within a defined window
- Sensitive files are delivered through signed, expiring, tenant-safe access paths
- MFA and active-session controls exist for high-risk portal users
- Compliance/trust pages are present and linked from the product shell
- Monitoring, backup, restore, and controlled feature rollout practices are documented and testable

---

## Out of scope for Phase 8

- AI matching and copilot experiences
- Public developer API and webhook platform
- Revenue-facing monetization experiments beyond the Phase 5 billing scope
