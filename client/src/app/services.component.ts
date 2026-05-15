import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Marketing /services page. Story-45 design tokens. Each portal gets a
 * concrete bullet list of what's actually shipped so a visitor can map
 * "do you have X?" to a real link.
 */
@Component({
  selector: 'app-services',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="page">
      <header class="hero">
        <p class="eyebrow">Services</p>
        <h1>What each portal actually does.</h1>
        <p class="lede">
          Quantam Analytics is shaped as five role-specific portals plus an
          admin and integration layer. Every feature below is implemented
          end-to-end on the platform today and reachable from the link on
          the row.
        </p>
      </header>

      <section class="row" aria-labelledby="recruiter-heading">
        <header>
          <h2 id="recruiter-heading">Recruiter portal</h2>
          <a routerLink="/recruiter" class="open">Open recruiter →</a>
        </header>
        <ul>
          <li>Dashboard with stuck-stage SLA surfacing + recent activity feed.</li>
          <li>Pipeline kanban with drag-and-drop status moves and a keyboard "Move…" menu for ADA.</li>
          <li>Candidate search across name, email, headline, location, and tags with saved filter presets.</li>
          <li>Bulk tag + status updates on selected applications.</li>
          <li>Email-template editor with merge fields and live preview at <code>/recruiter/email-templates</code>.</li>
          <li>Resume parser at <code>/recruiter/resume-parser</code> that extracts name, contact, skills, and work history.</li>
          <li>Recycle bin at <code>/recruiter/recycle-bin</code> with a 30-day restore window.</li>
          <li>Invoice-handoff CSV download for QuickBooks reconciliation.</li>
        </ul>
      </section>

      <section class="row" aria-labelledby="contractor-heading">
        <header>
          <h2 id="contractor-heading">Contractor portal</h2>
          <a routerLink="/contractor" class="open">Open contractor →</a>
        </header>
        <ul>
          <li>Weekly timesheet submission with hour categories (regular, overtime, holiday, PTO).</li>
          <li>Contractor invoice submission with line items, period dates, and notes.</li>
          <li>Status visibility from Draft → Submitted → Approved → Paid.</li>
          <li>Audit trail showing exactly who approved or rejected and when.</li>
        </ul>
      </section>

      <section class="row" aria-labelledby="client-heading">
        <header>
          <h2 id="client-heading">Client portal</h2>
          <a routerLink="/client" class="open">Open client →</a>
        </header>
        <ul>
          <li>Read-only view of all jobs the staffing firm is recruiting on for this client.</li>
          <li>Signed-document inbox at <code>/client</code> for esign artifacts.</li>
          <li>One-click approve / reject of contractor invoices with optional note.</li>
          <li>Tenant-isolated by default — a client only ever sees their own data.</li>
        </ul>
      </section>

      <section class="row" aria-labelledby="candidate-heading">
        <header>
          <h2 id="candidate-heading">Candidate portal</h2>
          <a routerLink="/candidate" class="open">Open candidate →</a>
        </header>
        <ul>
          <li>Profile editor with full-name, headline, summary, and skills.</li>
          <li>Application timeline showing every status change and recruiter comment.</li>
          <li>Resume upload with size + type allowlist and a parsed-fields preview.</li>
          <li>Quick actions tile linking to open jobs and (for contractor candidates) timesheets.</li>
          <li>Signed-URL resume download — links expire in 5 minutes by default.</li>
        </ul>
      </section>

      <section class="row" aria-labelledby="interview-heading">
        <header>
          <h2 id="interview-heading">Interviews</h2>
          <a routerLink="/interviews" class="open">Open interviews →</a>
        </header>
        <ul>
          <li>Calendar provider abstraction over Google, Microsoft, and Zoom.</li>
          <li>Meeting-link generation with provider boundary so dev runs against a deterministic stub.</li>
          <li>Interviewer availability windows + 30-minute self-booking slot picker for candidates.</li>
          <li>Background-check (Checkr) and e-signature (DocuSeal) provider boundaries in the same shape.</li>
        </ul>
      </section>

      <section class="row" aria-labelledby="admin-heading">
        <header>
          <h2 id="admin-heading">Admin &amp; platform</h2>
          <a routerLink="/admin/audit" class="open">Open admin →</a>
        </header>
        <ul>
          <li>Audit review console — append-only log, queryable by actor, entity, and action.</li>
          <li>Granular RBAC matrix separating recruiter / HR / payroll / interviewer / manager.</li>
          <li>Sign-out-everywhere + suspicious-login feed landing pad for the Auth0 post-login Action.</li>
          <li>Customer-facing platform API at <code>/api/v1/public/*</code> with tenant-scoped Bearer keys.</li>
          <li>Outbound webhook subscriptions with HMAC-SHA256 signing and exponential backoff retry.</li>
          <li>Tenant subscription model with 14-day trial, plan / seat tracking, and entitlement gate.</li>
        </ul>
      </section>

      <section class="row" aria-labelledby="ai-heading">
        <header>
          <h2 id="ai-heading">AI copilot</h2>
          <a routerLink="/recruiter" class="open">Open recruiter →</a>
        </header>
        <ul>
          <li>Candidate ↔ job match scorecard with overall, skills coverage, seniority fit, and gap analysis.</li>
          <li>Auto-generated candidate summary with 3-4 highlight bullets.</li>
          <li>Interview question proposals tuned to the job description.</li>
          <li>Onboarding-checklist generator covering hardware, accounts, intros, pulse + review.</li>
          <li>Offer-letter generator with state machine (Drafted → Sent → Accepted / Declined / Withdrawn).</li>
        </ul>
      </section>

      <section class="row" aria-labelledby="trust-heading">
        <header>
          <h2 id="trust-heading">Trust &amp; compliance</h2>
          <a routerLink="/trust" class="open">Open trust center →</a>
        </header>
        <ul>
          <li>Tenant isolation enforced at the data layer via EF Core global query filter.</li>
          <li>Append-only <code>audit_log_entries</code> on every mutation via SaveChangesInterceptor.</li>
          <li>Signed-URL downloads for private files (resumes, signed documents) with 1-hour TTL cap.</li>
          <li>MFA enforced for production sign-ins by Recruiter / Client / PlatformAdmin roles.</li>
          <li>Published trust center: <a routerLink="/privacy">privacy</a>, <a routerLink="/terms">terms</a>, <a routerLink="/security">security</a>, <a routerLink="/dpa">DPA</a>, <a routerLink="/accessibility">accessibility</a>.</li>
          <li>WCAG 2.1 AA verified via axe accessibility audit + a CI gate.</li>
        </ul>
      </section>
    </main>
  `,
  styles: `
    :host { display: block; }
    .page { width: min(1040px, 100%); margin: 0 auto; padding: 2rem 1.5rem 4rem; }
    .eyebrow { color: var(--color-primary, #1a3a8f); font-size: 12px; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; margin: 0 0 0.5rem; }
    .hero h1 { margin: 0 0 0.75rem; font-size: clamp(2rem, 3vw, 2.5rem); letter-spacing: -0.02em; }
    .hero .lede { color: var(--color-fg-muted, #5d6577); max-width: 60ch; margin: 0 0 2rem; font-size: 1.05rem; line-height: 1.65; }

    .row {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 12px;
      padding: 1.5rem;
      margin-bottom: 1rem;
    }
    .row > header {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 1rem;
      flex-wrap: wrap;
      margin-bottom: 0.75rem;
    }
    .row h2 { margin: 0; font-size: 1.15rem; }
    .row .open {
      color: var(--color-primary, #1a3a8f);
      font-weight: 600;
      font-size: 0.9rem;
      text-decoration: none;
    }
    .row .open:hover { text-decoration: underline; }
    .row ul {
      margin: 0;
      padding-left: 1.25rem;
      display: grid;
      gap: 0.4rem;
      font-size: 0.95rem;
      line-height: 1.55;
      color: var(--color-fg, #102046);
    }
    .row code {
      background: var(--color-canvas, #f5f7fb);
      padding: 0.1rem 0.35rem;
      border-radius: 4px;
      font-size: 0.85em;
    }
    .row a {
      color: var(--color-primary, #1a3a8f);
      text-decoration: underline;
    }
  `,
})
export class ServicesComponent {}
