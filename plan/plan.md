# Quantam Analytics — Staffing SaaS · Multi-Phase Plan

A multi-tenant staffing platform: ATS + CRM-lite + interview management + onboarding + timesheets + invoicing + outbound job-board posting. Built first for a single firm, designed from day 1 to white-label as SaaS.

## Stack (locked)

| Layer | Choice | Why |
|-------|--------|-----|
| API | ASP.NET Core 10 LTS | Existing expertise, supported through Nov 2028 |
| ORM | EF Core 10 + Npgsql | Mature, portable, same LINQ surface as SQL Server |
| Database | PostgreSQL | Free tiers everywhere (Neon, Supabase), portable across clouds |
| Background jobs | Hangfire | In-process, no extra infra, .NET-native |
| Frontend | Angular (LTS) | Existing expertise, single SPA with route-based portals |
| Auth | Auth0 free tier (or Azure B2C) | 7,500 MAU free, role-based, no roll-your-own |
| File storage | Cloudflare R2 | 10 GB free, $0 egress |
| Hosting (API) | Azure Container Apps | Scales to zero, ~$0 idle |
| Hosting (web) | Azure Static Web Apps | Free tier |
| Email (transactional) | Resend | 3K/mo free |
| Email (mailboxes) | Cloudflare Email Routing | $0 forward to existing inbox |
| CI/CD | GitHub Actions | 2,000 min/mo free |
| Infra-as-code | Bicep | Native to Azure, simpler than Terraform for this scope |
| Observability | Application Insights | Free tier sufficient until paying customers |

**Operating principle:** stay on LTS-only dependencies, managed services everywhere, single deployable monolith with clean module boundaries (split to services only if a real bottleneck appears).

## Multi-tenant strategy

Single database, `tenant_id` column on every tenant-scoped table, EF Core global query filter enforces isolation. Tenant resolved from JWT claim. PostgreSQL Row-Level Security as a defense-in-depth backstop. Schema-per-tenant or DB-per-tenant only if a single enterprise customer demands isolation.

---

## Phase Roadmap

### Phase 1 — MVP (target: 3 months)
Public marketing site, public job board, candidate signup + apply, recruiter portal with jobs CRUD and applications pipeline. **One staffing firm can run their full sourcing and screening pipeline on this.**

Phase 1 is split into **12 small PRs**, see [`phase-1-deliverables.md`](./phase-1-deliverables.md).

### Phase 2 — Time & Money (target: 2 months after Phase 1)
- Timesheets: weekly entry, client approval flow, PTO, overtime rules
- Invoicing: push timesheet data to QuickBooks Online API
- Stripe Billing for client invoicing where QBO isn't required
- Contractor portal: view paystubs, submit timesheets, request PTO

Phase 2 is split into **8 small PRs**, see [`phase-2-deliverables.md`](./phase-2-deliverables.md).

Outcome: **the platform replaces the spreadsheets and Bullhorn-lite tools that staffing firms pay $300–500/seat/month for.**

### Phase 3 — Hiring Workflow Depth (target: 2 months after Phase 2)
- BGC integration: Checkr API (initiate check, webhook for status)
- Interview management: calendar sync (Google + Outlook), Zoom/Teams meeting creation, scorecard templates, question bank
- E-sign: DocuSeal (open source, self-hosted) for offer letters and onboarding
- I-9 / W-4 / direct deposit forms collection

Outcome: **end-to-end placement — sourced → screened → interviewed → background-checked → offered → onboarded — all inside the platform.**

### Phase 4 — Distribution & Client Visibility (rolling)
- Outbound job-board posting:
  - Indeed XML feed (cheapest, most reach)
  - Dice posting API (with partner contract)
  - LinkedIn deferred — RSC partnership only after revenue justifies it
- Client portal: jobs they've requested, candidates submitted, status visibility, document sharing
- Reporting: time-to-fill, source-of-hire, recruiter activity, gross margin per placement

### Phase 5 — SaaS Productization (only if Phase 1–4 produce traction)
- Tenant signup self-serve flow
- Subscription billing (Stripe)
- White-label per tenant (logo, primary color, custom subdomain)
- SOC 2 Type II preparation (audit logs, access reviews, encryption attestations)
- Marketing site for the SaaS (separate from any one tenant's site)

Phase 5 is partially complete today: the plan handoff and SOC 2 baseline landed,
but the real provider integrations, production hardening, self-serve tenant
signup, and subscription billing remain open in
[`phase-5-deliverables.md`](./phase-5-deliverables.md).

### Phase 6 — Product Polish & UX MVP (pulled forward)
- Shared design system, typography, and accessible component primitives
- Global shell with role-aware navigation, footer, skip-link, and mobile menu
- Redesigned recruiter / client / candidate / contractor portals with meaningful demo data
- Missing employee workflow surfaces (invoice submission, working details, invoice queues)
- Accessibility audit and CI enforcement

Phase 6 was pulled ahead of the remaining Phase 5 work because the platform had
the right engineering foundation but not a demo-ready product surface. That UX
catch-up is now complete; see [`phase-6-deliverables.md`](./phase-6-deliverables.md).

### Phase 7 — Recruiting Operations Maturity
- Candidate timelines, activity feeds, internal collaboration, and recruiter notes
- Drag-and-drop pipeline movement, stage SLAs, and bulk workflow actions
- Resume parsing, advanced search, tags, templates, and portal transparency
- Notification center, recruiter analytics, and polished loading/empty/error states

Phase 7 converts the current product from "well-engineered demo" into a daily
recruiter operating surface. See [`phase-7-deliverables.md`](./phase-7-deliverables.md).

### Phase 8 — Enterprise Control Plane
- Granular RBAC and tenant-safe permission matrices
- Audit log UI, access reviews, soft delete, and record recovery
- Signed file delivery, MFA, session management, and stronger auth controls
- Trust-center pages, observability, backup/restore runbooks, feature flags

Phase 8 is the trust and governance layer enterprise buyers will expect once
the recruiter workflows are mature. See [`phase-8-deliverables.md`](./phase-8-deliverables.md).

### Phase 9 — Intelligence, Integrations & Monetization
- AI candidate matching and recruiter copilot experiences
- Interview scheduling automation and provider-backed self-booking
- Offer generation, customer-facing APIs, outbound webhooks, and billing UX

Phase 9 is the leverage phase: make the product harder to copy, easier to
integrate, and more monetizable. See [`phase-9-deliverables.md`](./phase-9-deliverables.md).

---

## Compliance baseline (built in from day 1)

- **Audit logs** for every mutation on tenant-scoped data
- **Encryption at rest** for all blob storage (resumes, contracts)
- **Encryption in transit** — TLS everywhere, no exceptions
- **Data deletion** workflows for GDPR/CCPA right-to-be-forgotten
- **Access control** — RBAC with deny-by-default; never trust client-supplied tenant_id
- **Secrets management** — Azure Key Vault, never in env files committed to git
- **Dependency scanning** — Dependabot enabled, weekly review

These are non-negotiable and must be present from PR-01 architecture decisions onward, not retrofitted.

---

## Operating cadence

- **Branch + PR per deliverable.** Branch name: `qa001-<short-scope>` (e.g., `qa001-bootstrap`, `qa001-solution-scaffold`). Never push directly to `main`.
- **Squash-merge** PRs into `main`. `main` is always deployable.
- **Tag releases** `vMAJOR.MINOR.PATCH` to trigger production deploys (later phases).
- **Update CLAUDE.md and README.md** at the end of every milestone PR (currently every Phase boundary).
- **One small PR at a time** so review stays cheap and rate-limit / context budget stays healthy.

---

## Domain & cost target

- Domain: `quantamanalitics.com` (already owned)
- Email: migrate off Google Workspace ($70/mo) to Cloudflare Email Routing (free) once Phase 1 deploys
- Build phase total run cost: **$0–10/month**
- Production launch target: **<$50/month** until paying customers exist
