# Phase 5 — Production Readiness, Real Integrations & SaaS Productization

Phase 5 differs from earlier phases in shape. Phases 1–4 were "code-only,
straight-line, ship a small PR a day". Phase 5 is mostly real handshakes
with real external services — every story below has a **prerequisite**
section that lists what you need to set up outside the repo before the
code PR can be merged. Treat the prerequisites as gating: don't open the
PR until the prerequisite is done.

Three epics, ten stories. One story = one PR. Same `qa001-<scope>`
branch convention as before.

> **Already in this phase:** PR-44 (`qa001-soc2-baseline`, audit-log
> interceptor) is in review. Listed below as Story B4.

---

## Epic A — Real Provider Integrations

Replace the Phase 3 / 4 stub provider clients with real partner-API
calls. Same provider-boundary pattern stays in place; only the
implementation changes. Tests + tenant-isolation IT keep passing.

### Story A1 — Real Checkr client + signed webhooks

- **Branch:** `qa001-checkr-real`
- **Status:** pending
- **Sub-tasks**
  - Add Checkr .NET SDK (or hand-rolled `HttpClient`) to Infrastructure
  - New `CheckrClient` implementing `ICheckrClient` against the sandbox
  - Add `CheckrWebhookVerifier` that validates `X-Checkr-Signature` HMAC
  - Update `BackgroundCheckEndpoint` webhook landing pad to call the
    verifier before any DB write
  - Retire `StubCheckrClient` (delete file, drop DI registration)
- **Acceptance**
  - `IBackgroundCheckRequest` round-trips against Checkr sandbox in dev
  - Invalid HMAC returns 400; missing header returns 400
  - Existing `BackgroundCheckTests` pass; existing tenant-isolation IT
    pattern catches any cross-tenant regression
- **Prerequisites (you do these first)**
  - Checkr sandbox account + API key
  - Webhook signing secret
  - Both stored in GitHub secrets: `CHECKR_API_KEY`,
    `CHECKR_WEBHOOK_SECRET`
- **Estimated size:** ~250 lines

### Story A2 — Real DocuSeal client + signed webhooks

- **Branch:** `qa001-docuseal-real`
- **Status:** pending
- **Sub-tasks**
  - DocuSeal HTTP client implementing `IDocuSealClient`
  - `DocuSealWebhookVerifier` for `X-DocuSeal-Signature`
  - Update `EsignDocumentEndpoint` webhook landing pad
  - Retire `StubDocuSealClient`
- **Acceptance**
  - Submission created in DocuSeal cloud (or self-hosted) for a real
    template
  - Lifecycle webhooks (`sent`, `viewed`, `signed`) update the
    `EsignDocument` row
  - Same isolation + status-machine tests pass
- **Prerequisites**
  - DocuSeal instance (cloud or self-hosted) + API token
  - Webhook signing secret in `DOCUSEAL_API_TOKEN`,
    `DOCUSEAL_WEBHOOK_SECRET`
  - At least one offer-letter template configured with the slug
    `offer_letter_v1` matching the seed data
- **Estimated size:** ~250 lines

### Story A3 — Real Zoom meeting links

- **Branch:** `qa001-meeting-link-zoom`
- **Status:** pending
- **Sub-tasks**
  - Zoom Server-to-Server OAuth client in Infrastructure
  - `ZoomMeetingLinkProvider` implementing the GoogleCalendar branch of
    `IMeetingLinkGenerator`
  - Catalog change so GoogleCalendar provider routes to Zoom (Outlook
    still uses the deterministic stub until A4)
- **Acceptance**
  - Recruiter clicking "create meeting link" on a Google-calendar event
    gets a real `https://zoom.us/j/...` URL back
  - Token refresh handled (Server-to-Server OAuth tokens expire hourly)
- **Prerequisites**
  - Zoom Marketplace Server-to-Server OAuth app
  - `ZOOM_ACCOUNT_ID`, `ZOOM_CLIENT_ID`, `ZOOM_CLIENT_SECRET` in
    GitHub secrets
- **Estimated size:** ~200 lines

### Story A4 — Real Microsoft Teams meeting links

- **Branch:** `qa001-meeting-link-teams`
- **Status:** pending
- **Sub-tasks**
  - Microsoft Graph API client in Infrastructure (use the official SDK)
  - `TeamsMeetingLinkProvider` implementing the OutlookCalendar branch
  - Catalog routes OutlookCalendar to Teams; retire deterministic stub
    fully (only the test stub remains)
- **Acceptance**
  - Meeting links return `https://teams.microsoft.com/l/meetup-join/...`
    URLs
  - The Graph app's tenant + scopes are documented in `docs/auth.md`
- **Prerequisites**
  - Azure AD app registration with `OnlineMeetings.ReadWrite.All`
    application permission
  - Admin consent granted in the target tenant
  - `TEAMS_TENANT_ID`, `TEAMS_CLIENT_ID`, `TEAMS_CLIENT_SECRET`
- **Estimated size:** ~250 lines

### Story A5 — Real Dice posting (gated on partner contract)

- **Branch:** `qa001-dice-real`
- **Status:** pending (gated)
- **Sub-tasks**
  - Real Dice posting API client (only doable once a partner contract
    is in place)
- **Acceptance**
  - Recruiter "post to Dice" path emits a real Dice job posting
- **Prerequisites**
  - Dice partner agreement + API access (revenue-gated; this story
    can sit unstarted indefinitely without blocking anything else)
- **Estimated size:** ~200 lines

---

## Epic B — Production Readiness

Move the platform off "shared dev with maintainer-only access" and onto
something a paying customer's compliance team would accept.

### Story B1 — Private GHCR + managed-identity image pull

- **Branch:** `qa001-ghcr-private`
- **Status:** pending
- **Sub-tasks**
  - Flip the GHCR repo's visibility to private
  - Grant the Container App's system-assigned managed identity
    `read:packages` access to the GHCR repo
  - Bicep: Container App `registries` block points at GHCR with the
    managed-identity auth shape
  - Workflow stops needing any GHCR PAT
- **Acceptance**
  - `docker pull` from anonymous fails 401
  - Container App revisions still come up clean
- **Prerequisites**
  - None — this is config + Bicep work I can drive once you say "go"
- **Estimated size:** ~80 lines (mostly Bicep)

### Story B2 — Production Auth0 tenant + MFA

- **Branch:** `qa001-auth0-prod`
- **Status:** pending
- **Sub-tasks**
  - Provision new Auth0 tenant `quantamanalitics.us.auth0.com`
  - Replicate the dev tenant's Action that emits `roles` + `tenant_id`
    custom claims
  - Enable MFA + breach-password detection
  - Bicep / workflow vars switch to prod values when env=prd
  - `docs/auth.md` documents the new tenant
- **Acceptance**
  - Sign-in against prod Auth0 succeeds
  - MFA challenge presents on every login
  - Dev tenant unaffected
- **Prerequisites**
  - Production Auth0 tenant created (you do this in the Auth0 dashboard)
  - Production tenant's `AUTH0_DOMAIN`, `AUTH0_AUDIENCE`,
    `AUTH0_CLIENT_ID` captured as GitHub vars / secrets
- **Estimated size:** ~150 lines

### Story B3 — Custom domain + managed certs

- **Branch:** `qa001-custom-domain`
- **Status:** pending
- **Sub-tasks**
  - DNS records (Cloudflare): `quantamanalitics.com` → SWA,
    `api.quantamanalitics.com` → Container App
  - SWA custom-domain binding
  - Container App custom-domain + managed-cert binding
  - CORS allow-list includes both the SWA hostname and
    `quantamanalitics.com`
  - `PublicWeb__BaseUrl` + Indeed feed URL update to
    `https://quantamanalitics.com`
- **Acceptance**
  - Both URLs serve over HTTPS with valid managed certs
  - Indeed feed `<url>` elements point at `quantamanalitics.com/jobs/...`
- **Prerequisites**
  - Domain is owned (already true)
  - DNS access at the registrar / Cloudflare
- **Estimated size:** ~120 lines

### Story B4 — SOC 2 audit-log baseline

- **Branch:** `qa001-soc2-baseline`
- **Status:** **in review** (this is PR-44)
- **Sub-tasks** — done
- **Acceptance** — see PR description
- **Estimated size:** 639 lines actual

### Story B5 — Branch protection + required CI checks

- **Branch:** `qa001-branch-protection`
- **Status:** pending
- **Sub-tasks**
  - Confirm branch protection on `main` requires the four CI checks
    (`API · build + test`, `Client · build + lint`, `Infra · bicep lint`,
    `API · docker build (no push)`)
  - Confirm "Require linear history" is on (matches squash-merge convention)
- **Acceptance**
  - Direct push to `main` is rejected
  - PRs cannot be merged until all four checks pass
- **Prerequisites**
  - GitHub admin access to repo Settings → Branches (you have it)
- **Estimated size:** zero lines (settings change), but capture as a
  short ADR in `docs/adr/` so the convention is recorded

---

## Epic C — SaaS Productization

Self-serve customer onboarding + recurring billing — the things that
turn the platform from "a tool one firm runs" into a SaaS.

### Story C1 — Self-serve tenant signup

- **Branch:** `qa001-tenant-signup`
- **Status:** pending
- **Sub-tasks**
  - `POST /api/v1/signup` — anonymous endpoint accepting org name +
    admin email + slug
  - Provision `Tenant` row + first recruiter `User` via Auth0 Management
    API
  - Send welcome email via Resend with a magic link to set the password
  - SPA `/signup` route + form
  - Rate-limit the endpoint per source IP (basic anti-abuse)
- **Acceptance**
  - A new staffing firm can fill the form, click the email link, sign in,
    post their first job — all without a maintainer in the loop
- **Prerequisites**
  - `RESEND_API_KEY` in GitHub secrets, sender domain verified
  - Auth0 Management API M2M app + token; `AUTH0_MGMT_*` secrets
  - Decide whether new tenants get a default Auth0 `Recruiter` role
    immediately or a pending "verify email first" stage (recommend the
    latter)
- **Estimated size:** ~400 lines

### Story C2 — Stripe customer + subscription model

- **Branch:** `qa001-stripe-baseline-real`
- **Status:** pending
- **Sub-tasks**
  - Stripe .NET SDK in Infrastructure
  - `TenantBilling` entity (tenant-scoped, 1:1 with Tenant): Stripe
    customer id, subscription status, current period end
  - Real Stripe customer created on tenant signup (extends C1)
  - `/api/v1/admin/billing/checkout` returns a Stripe Checkout session
  - `/api/v1/webhooks/stripe` landing pad with HMAC verification on the
    signing secret
  - Webhook updates `TenantBilling.SubscriptionStatus` from
    `customer.subscription.updated` events
- **Acceptance**
  - Tenant admin can land on a Stripe-hosted checkout, complete a test
    payment, return to the SPA
  - Webhook flips status to `Active` within seconds of payment
- **Prerequisites**
  - Stripe account
  - Product + Price configured (recommend per-recruiter-seat per month)
  - `STRIPE_API_KEY`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_ID`
- **Estimated size:** ~500 lines (entity + endpoints + webhook + tests)

### Story C3 — Subscription enforcement + dunning

- **Branch:** `qa001-stripe-enforcement`
- **Status:** pending
- **Sub-tasks**
  - Middleware that 402-rejects mutations when `TenantBilling.Status`
    is `PastDue` or `Canceled` (reads stay open so the tenant can see
    their own data)
  - Resend dunning email at status change to `PastDue`
  - SPA banner when `PastDue`, with a re-checkout button
- **Acceptance**
  - A tenant whose subscription lapses gets locked out of writes within
    a few seconds of the Stripe event
  - Re-payment unlocks it
- **Prerequisites** — Story C2 done
- **Estimated size:** ~250 lines

---

## Phase 5 Definition of Done

All ten stories above either merged or explicitly deferred (Story A5
defers indefinitely without blocking the phase). Specifically:

- All three core stub providers (Checkr / DocuSeal / Zoom + Teams) are
  retired in favor of real API calls
- Production runs against private GHCR, prod Auth0 with MFA, and a
  custom domain over managed certs
- Audit log captures every tenant-scoped mutation (B4 — done in PR-44)
- Branch protection on `main` enforces the four CI checks (B5)
- A new staffing firm can self-serve sign up, get billed via Stripe,
  and have writes gated by subscription status
- README, CLAUDE.md, and `docs/verification.md` reflect the post-Phase-5
  reality

---

## Out of scope for Phase 5 (deferred)

- LinkedIn job-board posting (RSC partnership; revenue-gated, separate
  phase or one-off PR when a contract exists)
- Custom report builder / saved views — the reporting summary is enough
  baseline for the phase
- White-label per tenant beyond logo + primary color (custom subdomain
  CNAME flow ships later when a customer needs it)
- Schema-per-tenant or DB-per-tenant isolation (only if an enterprise
  customer demands it; the global-query-filter approach scales to many
  tenants on one DB)
- Mobile apps
- SOC 2 Type II audit itself (the baseline lands here; the audit is a
  human / vendor process, not a code PR)
