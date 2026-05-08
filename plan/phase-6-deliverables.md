# Phase 6 — Product Polish & UX MVP

**Status: COMPLETE.** All 5 stories shipped to `main`. Phase 6 pulled the
product presentation up to the level of the underlying engineering work:
shared tokens, a global shell, redesigned portals, missing employee workflow
surfaces, and an accessibility pass all landed before the repo returns to the
remaining Phase 5 real-integration backlog.

> **Why this phase exists.** Phases 1–5 built correctness underneath:
> multi-tenant data, auth, EF migrations, real-deploy pipeline, audit log,
> and the API surface that all four portals call. The *visible* product
> never got designed — every screen is a generic card grid with empty
> states and placeholder copy. Demoing the live URL today doesn't read as
> "industry-grade SaaS" because it isn't yet.
>
> Phase 6 is the catch-up. Convert plumbing into product: one design
> system, one global shell, four polished portals, the missing employee
> capabilities (invoices, working details), and a real accessibility
> audit. None of the Phase 5 integration work (Checkr, DocuSeal, Stripe)
> starts until this lands — there's no point wiring real billing into a
> UI a customer wouldn't put in front of their team.

One epic, five stories. Same `qa001-` branch convention.

| #   | Branch                                   | Scope                                                                  | Status  |
| --- | ---------------------------------------- | ---------------------------------------------------------------------- | ------- |
| 45  | `qa001-design-system`                    | Color palette, type, spacing, accessible component primitives          | merged  |
| 46  | `qa001-global-shell`                     | Logo, header nav, breadcrumbs, search, footer, skip-link, mobile menu | merged  |
| 47  | `qa001-portal-redesigns`                 | Apply design system to every portal + seed meaningful demo data        | merged  |
| 48  | `qa001-employee-capabilities`            | Contractor invoice submission, working details, client invoice queue   | merged  |
| 49  | `qa001-accessibility-audit`              | WCAG 2.1 AA pass, axe / Lighthouse in CI, mobile responsiveness        | merged  |

> Order matters. Story 45 ships the tokens every later story depends on.
> Story 46 (shell) depends on 45. Story 47 (portal redesigns) and 48
> (new capabilities) both depend on 45 + 46 but are independent of each
> other. Story 49 (a11y audit) goes last to verify the whole result.

---

## Epic — UX MVP & Missing Employee Capabilities

The shape of "good" we're aiming at:

- **Visual identity** — white + deep blue palette, calm and government-
  sober (the SBCounty / California-government look the user referenced
  as the target). High contrast everywhere. No colors fighting each other.
- **Typography** — one type family at three sizes, line-height that
  reads at 320px wide and 1440px wide without needing a zoom.
- **Navigation** — every page knows where it is (breadcrumb), what's
  around it (header nav), how to escape (footer + sitemap), and how to
  search (global search box).
- **Content density** — every empty state has either useful seeded data
  or a clear "you'd see X here once Y happens" call to action. Nothing
  ships as a card titled "(empty)".
- **Capabilities** — the contractor experience the user described in
  the original brief (submit timesheets, submit invoices, log working
  details) is fully wired, not implied.
- **Accessibility** — WCAG 2.1 AA at minimum, ARIA where semantic HTML
  isn't enough, keyboard works everywhere, screen reader announces
  route changes, focus is never lost.

---

## Story 45 — Design system & accessibility foundation

- **Branch:** `qa001-design-system`
- **Estimated size:** ~400 lines (Tailwind config + a dozen component
  primitives + a `/style-guide` reference route).
- **What ships**
  - Tailwind config with **design tokens**:
    - Color: `bg-canvas` (off-white), `bg-surface` (white),
      `text-ink-strong` / `text-ink-muted`, `accent-primary` (deep
      blue around `#1a3a8f`), `accent-primary-hover`, `accent-success`,
      `accent-warning`, `accent-danger`. All pairs verified against
      WCAG 2.1 AA contrast (4.5:1 body, 3:1 large).
    - Typography: Inter from Google Fonts (or system stack as
      fallback). Type scale `display`, `h1–h4`, `body`, `caption`.
      Line-height tuned for readability.
    - Spacing scale: `0.5 / 1 / 1.5 / 2 / 3 / 4 / 6 / 8` tied to a
      `4px` base.
    - Radii: `sm / md / lg`. Shadows: `sm / md` only — no chrome.
  - Component primitives, each with a sane default + accessible markup:
    - `qa-button` (primary / secondary / ghost / destructive)
    - `qa-input` / `qa-textarea` / `qa-select` (with label, hint,
      error states)
    - `qa-card` (header / body / footer slots)
    - `qa-alert` (info / success / warning / error)
    - `qa-modal` (focus trap, escape closes, ARIA roledialog)
    - `qa-tabs` (keyboard arrow-key nav, ARIA roletablist)
    - `qa-table` (sortable header, sticky header on scroll)
    - `qa-badge` (status pill, color tied to semantic meaning)
    - `qa-empty-state` (icon + title + description + optional action)
  - `/style-guide` route showing every primitive against every state,
    so designers and devs can review without spinning up the rest of
    the app.
- **Acceptance**
  - Every primitive has a visible focus ring meeting WCAG 2.1 AA.
  - Every text–background pair on `/style-guide` passes the contrast
    check (manually verified with WebAIM Contrast Checker for the first
    pass; CI gate added in Story 49).
  - No raw `<button>` / `<input>` left in the SPA outside the primitives
    (search-and-replace audit).
- **Out of scope for this story**
  - Replacing the shell or any portal screens — that's stories 46 / 47.

---

## Story 46 — Global navigation shell

- **Branch:** `qa001-global-shell`
- **Estimated size:** ~350 lines.
- **What ships**
  - **Header.** Left: logo + tenant name. Center: primary nav, role-
    aware (recruiter sees Jobs / Candidates / Submissions / Reports;
    client sees Jobs / Submissions / Documents / Approvals; candidate
    sees Profile / Applications; contractor sees Timesheets / Invoices
    / Pay history). Right: user menu with name + avatar + sign-out.
  - **Breadcrumbs.** Auto-generated from Angular route data; visible
    on every authenticated page below the header. "Home › Recruiter ›
    Applications › John Doe".
  - **Global search.** Input in the header, opens a panel on focus.
    Phase 6 ships UI only; the actual cross-entity search is a Phase 7
    item (real backend search endpoint behind it).
  - **Footer.** Company short blurb, sitemap (Home / About / Services /
    Privacy / Contact), accessibility statement link, year, social
    placeholder icons. Same on every page.
  - **Skip-to-content link.** Visible on Tab from page top, jumps to
    `#main`. Required for screen-reader users.
  - **Mobile.** Hamburger menu at < 768px; primary nav slides in from
    the right; same items as desktop. Footer stacks. Header height
    adjusts.
  - **Logo.** Replace placeholder with a simple wordmark (SVG, text-
    based — "Quantam Analytics" with a small mark). Out of scope:
    custom logomark design — that's a designer task later.
- **Acceptance**
  - Reaching any portal from the marketing site is one click from the
    header.
  - On a 320px-wide viewport, the header doesn't overflow horizontally.
  - The footer is keyboard-reachable from the header without trapping.
  - Tabbing from the page top reveals the skip link before the logo.
- **Dependencies:** Story 45 (design system tokens + primitives).

---

## Story 47 — Portal redesigns + meaningful seeded data

- **Branch:** `qa001-portal-redesigns`
- **Estimated size:** ~600 lines (split into per-portal sub-PRs if it
  grows past that — the rule still applies).
- **What ships**
  - **Recruiter portal**
    - **Jobs.** Replace card grid with a filterable data table
      (title, location, applied count, posted date, published flag).
      Inline "publish / unpublish". Click row → detail sidebar with
      apps + submissions for that job.
    - **Applications.** Kanban board (Applied / Interviewing /
      Offer Sent / Hired / Rejected). Drag to move stage (writes
      via existing endpoint).
    - **Reports.** Visualize the existing summary endpoint (funnel
      bars, time-to-fill metric tile, recruiter activity list).
    - **KPIs strip at top.** Open jobs, applicants this week,
      offers extended, hires. Reads from existing endpoints.
  - **Client portal**
    - **Jobs visibility** (existing endpoint, prettier table).
    - **Submissions queue** with accept / decline actions.
    - **Documents** read-only list of offer letters / packets.
    - **Timesheet approvals** (already wired) — apply new design.
  - **Candidate portal**
    - **Profile** with photo placeholder + resume upload status.
    - **Applications timeline** showing each app's stage history.
    - **Onboarding checklist** (existing) — apply new design.
  - **Contractor portal**
    - **Weekly timesheet** calendar view (Mon–Sun grid; click a day
      to enter hours). PR-43 entry already exists; this is the
      visual upgrade.
    - **Working details** — per-day notes, project / client tag.
      *Backed by Story 48 entities, but UI shape lives here.*
    - **Pay history.** List of paid weeks with amounts. Static
      until real payroll lands later.
  - **Demo seed data.** Update `DemoDataSeeder` so empty installs
    show 10+ jobs, 30+ candidates, applications spread across stages,
    interview events, submissions, signed offers, four weeks of
    timesheets per contractor. Every empty state in dev now has
    realistic content.
- **Acceptance**
  - Every portal's primary view contains data on the seeded dev
    tenant — no card titled "no items".
  - Drag-and-drop on the kanban moves applications and persists
    via the existing recruiter endpoint.
  - The contractor calendar view persists hours via the existing
    timesheet endpoints.
- **Dependencies:** Story 45 + 46.

---

## Story 48 — Missing employee capabilities

- **Branch:** `qa001-employee-capabilities`
- **Estimated size:** ~500 lines (entities + endpoints + UI).
- **What ships**
  - **`Invoice` entity** (tenant-scoped, contractor-owned).
    Fields: id, tenant_id, contractor_auth_subject, period_start,
    period_end, status (Draft / Submitted / Approved / Paid / Rejected),
    amount, hours, currency, notes, submitted_at, reviewed_at.
    State machine: Draft → Submitted → (Approved | Rejected); Approved →
    Paid.
  - **`WorkingDetailEntry` extension** of the existing `TimeEntry`.
    Per-day note, project tag, client tag, billable flag. Migration adds
    columns to the existing time_entries table — additive only.
  - **Endpoints**
    - `POST /api/v1/contractor/invoices` — submit invoice (RequireContractor)
    - `GET  /api/v1/contractor/invoices` — list own invoices
    - `GET  /api/v1/recruiter/invoices` — recruiter view across the tenant
    - `POST /api/v1/client/invoices/{id}/approve` — client approves
    - `POST /api/v1/client/invoices/{id}/reject` — client rejects
  - **UI surfaces**
    - Contractor: "Submit invoice" form on the contractor portal.
      Pre-populates from the most recent approved week. Shows status
      timeline once submitted.
    - Recruiter: invoice queue at `/recruiter/invoices`.
    - Client: invoice approval queue at `/client/invoices` (extends
      the existing approval flow shape).
  - **Tests** with the PR-30 PostgresFixture covering the invoice state
    machine + cross-tenant isolation.
- **Acceptance**
  - A contractor can sign in, see their submitted weeks, fill an
    invoice form, submit it, and see it move through Submitted →
    Approved.
  - A client can approve / reject and the contractor sees the new
    state.
  - Cross-tenant isolation IT covers the new endpoints.
- **Dependencies:** Story 45 (design primitives).

---

## Story 49 — Accessibility audit + mobile responsiveness pass

- **Branch:** `qa001-accessibility-audit`
- **Estimated size:** ~250 lines of fixes + a CI check.
- **What ships**
  - **CI gate.** Add `axe-core` or Lighthouse-CI to the `client` job
    in `ci.yml`. Fail the build if any page reports `serious` or
    `critical` violations.
  - **Manual passes**
    - Tab through every authenticated route. Focus is always visible
      and never lost on route change.
    - Screen-reader pass with VoiceOver on macOS for one full user
      journey per role.
    - Mobile pass at 320 / 768 / 1024 breakpoints — no horizontal
      overflow, no truncated text, every action is reachable.
  - **Findings fixed.** Common likely issues to clean up:
    - Form labels not programmatically associated with inputs
    - Color-only state signals (need icons / text too)
    - Missing `aria-live` regions for toasts and async updates
    - Focus management on Angular route changes (focus moves to `<h1>`
      of the new view)
  - **Accessibility statement page** at `/accessibility` linked from the
    footer, declaring the WCAG conformance level.
- **Acceptance**
  - `axe-core` CI check is required and green.
  - Lighthouse mobile score on `/`, `/jobs`, `/recruiter`,
    `/contractor` is ≥ 90 for Performance and ≥ 95 for Accessibility.
  - One screen-reader walkthrough recorded per portal so we have a
    baseline to compare against future regressions.
- **Dependencies:** Stories 45, 46, 47, 48 (audits the finished result).

---

## Phase 6 Definition of Done

All five stories merged. Specifically:

- A new visitor lands on `/`, signs in, and lands on a portal that
  looks like a 2025-era SaaS, not a wireframe.
- Every empty state has either real seeded data or a meaningful "what
  goes here once you do X" message.
- Contractors can submit timesheets *and* invoices and log working
  details. Clients can approve them. Recruiters can review them.
- Every page passes WCAG 2.1 AA via the CI axe gate.
- README + CLAUDE.md show screenshots of the polished portals.

---

## Out of scope for Phase 6 (deferred)

- Custom-designed logomark (we ship a wordmark; a designer can
  refine later)
- Dark mode (one palette is enough for v1)
- White-label per tenant (custom subdomain CNAME flow)
- Real cross-entity search backend (UI ships in Story 46; backend in
  a Phase 7 search PR)
- Notification center / email digests
- Real Checkr / DocuSeal / Stripe integrations — those resume in
  Phase 5 once Phase 6 is in review

---

## What we're NOT changing

- Auth model, multi-tenant query filter, audit-log capture, EF
  migrations, deploy pipeline. None of the underneath. This is a
  pure UI / capability layer.
- Endpoint routes already in production. New entities (Invoice,
  WorkingDetailEntry) get new routes; existing ones keep theirs.

---

## Open questions before we start

- **Logo / brand mark.** Do you have a wordmark or do we ship a
  text-only "Quantam Analytics" header as the placeholder? The latter
  is fast; the former needs a designer or a vector source from you.
- **Color palette.** "White + blue, like SBCounty" anchors the
  direction, but I'd like to confirm two specific blues — primary and
  hover — before we lock it in. I'll propose a pair in Story 45's PR
  and you sign off.
- **"Latest accessibility standards."** WCAG 2.1 AA is the standard
  legal floor (ADA case law has been settling there). Going to AAA
  costs roughly 2x and customers don't ask for it. AA is the target
  unless you tell me otherwise.
- **Demo data depth.** I'll seed 10 jobs / 30 candidates as a default;
  if you want more (or fewer) say so before Story 47 starts.
