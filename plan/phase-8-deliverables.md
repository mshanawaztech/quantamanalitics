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
| 60  | `qa001-audit-console`               | Queryable audit log UI, subject/history views, login/access review surfaces | merged |
| 61  | `qa001-soft-delete-recovery`        | Soft delete on `ITenantScoped`, recycle bin, restore window               | in progress |
| 62  | `qa001-file-security`               | `IResumeStorage.CreateSignedDownloadUrlAsync`, /me + /recruiter download-link endpoints, 1-hour TTL cap | review |
| 63  | `qa001-auth-hardening`              | `/me/security/sessions/revoke` + `/me/security/suspicious-login` endpoints + Auth0 dashboard playbook   | review |
| 64  | `qa001-trust-center`                | /trust, /privacy, /terms, /security, /dpa pages + footer column + breadcrumb labels                     | review |
| 65  | `qa001-ops-observability`           | `IFeatureGate` + `AppSettingsFeatureGate` + /me/features endpoint + `docs/ops-runbook.md`                | review |

> Order matters. PR-59 (granular RBAC) and PR-60 (audit console) should land
> before broader rollout of recoverability and trust-center features, because
> most enterprise buyers will evaluate permissioning and accountability first.

### Status snapshot

PR-58, 59, 60 are merged on main. PR-61 is in progress on the
`qa001-soft-delete-recovery` branch. PRs 62, 63, 64, 65 are committed
locally on their named branches and ready to push — they touch
disjoint files (storage layer, session endpoints + docs, Angular pages,
features infrastructure + docs) so they can be reviewed independently
of PR-61 without merge risk. No EF migrations were added by 62–65, so
no `dotnet ef` step is required before opening their PRs.

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
