# Phase 5 — SaaS Productization & Real Integrations · 8 Deliverables

**Status: IN PROGRESS.** Phase 5 was intentionally split in two: the long-tail
real integrations and SaaS productization work remain open, while the
foundational governance/safety slice already landed and the UX polish catch-up
was pulled forward as Phase 6. The active queue after Phase 6 is to return here
and finish the remaining provider, production-hardening, and self-serve tenant
work.

Phase 5 swaps the Phase 3 / 4 stub providers for real partner-API calls,
hardens the deployment for a paying-customer baseline, and adds the
self-serve productization layer that turns the platform into something
new tenants can sign up for without a maintainer in the loop.

Keep the same discipline as Phases 1–4: one branch, one PR, one tightly
scoped deliverable. Branch prefix stays `qa001-`.

| #   | Branch                                 | Scope                                                                  | Status  |
| --- | -------------------------------------- | ---------------------------------------------------------------------- | ------- |
| 37  | `qa001-phase5-plan`                    | Phase 5 deliverables board + repo state handoff from Phase 4           | merged  |
| 38  | `qa001-checkr-real`                    | Replace StubCheckrClient with real Checkr partner-API + signed webhooks | pending |
| 39  | `qa001-docuseal-real`                  | Replace StubDocuSealClient with real DocuSeal API + signed webhooks    | pending |
| 40  | `qa001-meeting-link-real`              | Real Zoom + Microsoft Teams meeting-link generation                    | pending |
| 41  | `qa001-prod-hardening`                 | Private GHCR pull via Container App registry creds (slice 1 of 3)       | review  |
| 42  | `qa001-tenant-signup`                  | Self-serve tenant signup flow + first-recruiter invite                 | pending |
| 43  | `qa001-stripe-subscriptions`           | Stripe subscription billing for tenant-level seats                     | pending |
| 44  | `qa001-soc2-baseline`                  | Audit logs, access reviews, encryption attestations for SOC 2 prep    | merged  |

> Order is a guideline. PR-37 (the plan) and PR-41 (prod hardening) should
> land **before** PR-42 / 43 / 44 — there's no point onboarding paying
> customers onto an environment that still uses the dev-tier auth tenant
> and a public GHCR image.

---

## How each deliverable runs

For every PR:

1. **Branch from `main`:** `git checkout main && git pull && git checkout -b qa001-<scope>`
2. **Stay inside the slice** — resist bundling adjacent partner-API / billing / SOC 2 work
3. **Commit small** with why-focused messages
4. **Open a PR** against `main` using the PR template
5. **Self-review** before merge
6. **Squash-merge** into `main`
7. **Update status** in this file after merge

If any deliverable grows beyond ~600 changed lines, split it before review.

---

## Definition of Done — Phase 5

Phase 5 ships when all of the following are true:

- All three external providers (Checkr, DocuSeal, Zoom/Teams) are calling
  real APIs in dev and survive their own webhook round-trips with HMAC
  signature verification
- Production runs against a private GHCR image pulled via managed identity
  and uses the prod Auth0 tenant with MFA + breach-password detection
- The platform serves traffic on a custom domain over a managed cert
- A new staffing firm can self-serve sign up, invite their first recruiter,
  and start posting jobs without a maintainer in the loop
- Stripe charges that tenant for seats on a recurring schedule
- Mutations on tenant-scoped data are captured in an audit log queryable
  per tenant and per subject for SOC 2 evidence
- README and CLAUDE.md reflect the post-Phase-5 reality

---

## Out of scope for Phase 5 (deferred)

- LinkedIn job-board posting (RSC partnership; revenue-gated)
- Custom report builder / saved views
- White-labeling per tenant beyond logo + primary color (custom subdomain
  CNAME flow ships separately when a customer needs it)
- Schema-per-tenant or DB-per-tenant isolation (only if an enterprise
  customer demands it)
- Mobile apps
