import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Phase 8 / Story 64 — trust center.
 *
 * Five sibling pages (overview, privacy, terms, security, DPA) live in
 * this file. Each is its own standalone component so the router can wire
 * them up individually, but they share a layout + token-driven styling
 * defined in {@link TrustLayoutComponent}. Content is intentionally
 * generic but specific enough to anchor the claims the platform actually
 * fulfills today — sentences that would be embarrassing if a customer
 * asked "show me where" land in the matching code references.
 */

const SHARED_STYLES = `
  .page { width: min(960px, 100%); margin: 0 auto; padding: 2.5rem 1.5rem 4rem; }
  .eyebrow {
    color: var(--color-primary, #1a3a8f);
    font-size: 12px;
    font-weight: 700;
    letter-spacing: 0.12em;
    text-transform: uppercase;
    margin: 0 0 0.5rem;
  }
  h1 { margin: 0 0 0.75rem; font-size: 2rem; }
  .lede { color: var(--color-fg-muted, #5d6577); max-width: 60ch; margin: 0 0 2rem; font-size: 1.05rem; }
  section { margin: 2rem 0; }
  section h2 { font-size: 1.25rem; margin: 0 0 0.5rem; }
  section p, section ul { color: var(--color-fg, #102046); }
  ul { padding-left: 1.25rem; }
  ul li { margin: 0.25rem 0; }
  .meta {
    margin-top: 3rem;
    padding-top: 1rem;
    border-top: 1px solid var(--color-border, #d8dde7);
    color: var(--color-fg-muted, #5d6577);
    font-size: 0.85rem;
  }
  .crosslinks {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin: 2rem 0;
  }
  .crosslinks a {
    display: inline-block;
    padding: 0.5rem 0.875rem;
    border: 1px solid var(--color-border, #d8dde7);
    border-radius: 999px;
    color: var(--color-primary, #1a3a8f);
    text-decoration: none;
    font-size: 0.875rem;
    font-weight: 600;
  }
  .crosslinks a:hover { background: var(--color-primary-soft, #e7ecf6); }
`;

const TRUST_LAST_REVIEWED = 'May 2026';

@Component({
  selector: 'app-trust-center',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="page">
      <p class="eyebrow">Trust center</p>
      <h1>How Quantam Analytics handles your data.</h1>
      <p class="lede">
        Quantam Analytics is a multi-tenant staffing platform. This page is
        the canonical entry point for the controls, attestations, and
        published policies that make that statement specific.
      </p>

      <section>
        <h2>What we publish</h2>
        <ul>
          <li>
            <strong><a routerLink="/privacy">Privacy notice</a></strong> — what
            we collect, why, and your rights as a candidate, contractor, or
            customer admin.
          </li>
          <li>
            <strong><a routerLink="/terms">Terms of service</a></strong> — the
            contract that governs use of the platform.
          </li>
          <li>
            <strong><a routerLink="/security">Security overview</a></strong> —
            tenant isolation, encryption, access reviews, audit log, signed
            downloads.
          </li>
          <li>
            <strong><a routerLink="/dpa">Data processing addendum</a></strong> —
            controller / processor commitments, sub-processors, transfer
            mechanism.
          </li>
        </ul>
      </section>

      <section>
        <h2>How we operate</h2>
        <ul>
          <li>Every tenant's data is isolated at the data layer with an
            enforced query filter. There is no API path that lets a
            caller spell another tenant's identifier.</li>
          <li>Mutations land in an append-only audit log scoped to the
            tenant and reviewable from the admin console.</li>
          <li>Private file downloads (resumes, signed documents) use
            short-lived signed URLs scoped to the recipient.</li>
          <li>MFA enforcement is on for production sign-ins to
            recruiter, client, and platform-admin roles.</li>
        </ul>
      </section>

      <section>
        <h2>Reporting an issue</h2>
        <p>
          Suspect a vulnerability? Email
          <a href="mailto:security@quantamanalitics.com">security&#64;quantamanalitics.com</a>.
          We acknowledge within one business day. Public-disclosure timeline
          and safe-harbor language live in the
          <a routerLink="/security">security overview</a>.
        </p>
      </section>

      <p class="meta">Last reviewed: ${TRUST_LAST_REVIEWED}.</p>
    </main>
  `,
  styles: SHARED_STYLES,
})
export class TrustCenterComponent {}

@Component({
  selector: 'app-privacy',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="page">
      <p class="eyebrow">Privacy</p>
      <h1>Privacy notice.</h1>
      <p class="lede">
        We collect only what we need to run the staffing workflow you
        signed up for, and we never sell candidate data.
      </p>

      <section>
        <h2>What we collect</h2>
        <ul>
          <li>Account fields — name, work email, role, tenant.</li>
          <li>Candidate profile — resume file, headline, summary, skills, contact details you provide.</li>
          <li>Application + timesheet activity tied to your tenant.</li>
          <li>Audit metadata — actor, tenant, action, timestamp — recorded on mutations for compliance.</li>
          <li>Operational signals — sign-in timestamps, browser type, IP for security events.</li>
        </ul>
      </section>

      <section>
        <h2>Why we collect it</h2>
        <ul>
          <li>To deliver the contracted staffing workflow.</li>
          <li>To meet our customers' record-keeping obligations.</li>
          <li>To investigate security incidents and abuse.</li>
        </ul>
      </section>

      <section>
        <h2>Your rights</h2>
        <p>
          You can ask for a copy of, correction to, or deletion of the data
          we hold about you. Email
          <a href="mailto:privacy@quantamanalitics.com">privacy&#64;quantamanalitics.com</a>.
          We respond within 30 days. Some records (audit log, financial
          ledger entries) are retained for regulatory reasons even after a
          deletion request — those exceptions are itemized in the
          <a routerLink="/dpa">DPA</a>.
        </p>
      </section>

      <div class="crosslinks">
        <a routerLink="/trust">Trust center</a>
        <a routerLink="/terms">Terms</a>
        <a routerLink="/security">Security</a>
        <a routerLink="/dpa">DPA</a>
      </div>

      <p class="meta">Last reviewed: ${TRUST_LAST_REVIEWED}.</p>
    </main>
  `,
  styles: SHARED_STYLES,
})
export class PrivacyComponent {}

@Component({
  selector: 'app-terms',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="page">
      <p class="eyebrow">Legal</p>
      <h1>Terms of service.</h1>
      <p class="lede">
        These terms apply to anyone who signs in to a Quantam Analytics
        tenant. Master service agreements with paying customers take
        precedence where they conflict.
      </p>

      <section>
        <h2>Acceptable use</h2>
        <ul>
          <li>Don't try to access another tenant's data.</li>
          <li>Don't scrape, reverse-engineer, or rate-limit-circumvent the platform.</li>
          <li>Don't upload content you don't have the right to share — including PII about candidates outside your tenant.</li>
        </ul>
      </section>

      <section>
        <h2>Availability</h2>
        <p>
          We target 99.9% monthly uptime for the production tier. Scheduled
          maintenance is announced in the admin console at least 48 hours
          ahead. Credits for missed targets are described in the master
          agreement; ad-hoc users on free tenants are best-effort.
        </p>
      </section>

      <section>
        <h2>Termination</h2>
        <p>
          Either party can terminate with 30 days notice. On termination we
          retain a read-only export of tenant data for 30 days, then delete
          (audit log retention exception described in the
          <a routerLink="/dpa">DPA</a>).
        </p>
      </section>

      <div class="crosslinks">
        <a routerLink="/trust">Trust center</a>
        <a routerLink="/privacy">Privacy</a>
        <a routerLink="/security">Security</a>
        <a routerLink="/dpa">DPA</a>
      </div>

      <p class="meta">Last reviewed: ${TRUST_LAST_REVIEWED}.</p>
    </main>
  `,
  styles: SHARED_STYLES,
})
export class TermsComponent {}

@Component({
  selector: 'app-security',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="page">
      <p class="eyebrow">Security</p>
      <h1>Security overview.</h1>
      <p class="lede">
        The controls below are the ones a buyer's security questionnaire
        usually asks about. Each one is either implemented in code or
        configured in the Auth0 / Azure dashboards we operate; nothing is
        aspirational on this page.
      </p>

      <section>
        <h2>Tenant isolation</h2>
        <p>
          Every tenant-scoped table carries a <code>tenant_id</code> column
          and an EF Core global query filter clamps every read to the
          current tenant. There is no public API that accepts a tenant id
          from the client.
        </p>
      </section>

      <section>
        <h2>Authentication</h2>
        <ul>
          <li>Auth0 issues every JWT; the API validates them on each request.</li>
          <li>MFA is enforced for production sign-ins by Recruiter, Client, and PlatformAdmin roles.</li>
          <li>Refresh tokens rotate with a 30-second reuse window.</li>
          <li>Suspicious sign-ins (new country, new device fingerprint) drop an in-app notification within minutes.</li>
        </ul>
      </section>

      <section>
        <h2>Audit log</h2>
        <p>
          Every mutation on tenant-scoped data is recorded in an append-only
          <code>audit_log_entries</code> table tagged with actor, tenant,
          entity, action, and timestamp. Admins can query their tenant's
          log from the audit review console (Phase 8 / Story 60).
        </p>
      </section>

      <section>
        <h2>File handling</h2>
        <ul>
          <li>Resumes and signed documents live in Cloudflare R2 with private ACLs.</li>
          <li>Downloads require a fresh 5-minute signed URL — direct R2 URLs are never served to the client.</li>
          <li>Uploads are size-limited (5 MB) and content-type allowlisted (PDF, DOC, DOCX, TXT).</li>
        </ul>
      </section>

      <section>
        <h2>Reporting a vulnerability</h2>
        <p>
          Email <a href="mailto:security@quantamanalitics.com">security&#64;quantamanalitics.com</a>.
          We acknowledge within one business day. Good-faith research has
          safe harbor as long as it doesn't access another tenant's data
          or degrade availability.
        </p>
      </section>

      <div class="crosslinks">
        <a routerLink="/trust">Trust center</a>
        <a routerLink="/privacy">Privacy</a>
        <a routerLink="/terms">Terms</a>
        <a routerLink="/dpa">DPA</a>
      </div>

      <p class="meta">Last reviewed: ${TRUST_LAST_REVIEWED}.</p>
    </main>
  `,
  styles: SHARED_STYLES,
})
export class SecurityComponent {}

@Component({
  selector: 'app-dpa',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="page">
      <p class="eyebrow">Data processing</p>
      <h1>Data processing addendum.</h1>
      <p class="lede">
        Standard DPA terms for customers who need a separate signed
        document. The text below is the binding language; paper copies on
        request from
        <a href="mailto:legal@quantamanalitics.com">legal&#64;quantamanalitics.com</a>.
      </p>

      <section>
        <h2>Roles</h2>
        <p>
          The customer is the data controller for personal data uploaded
          to their tenant. Quantam Analytics is the processor. We only
          process personal data on documented instructions from the
          customer — running the contracted staffing workflow is such an
          instruction.
        </p>
      </section>

      <section>
        <h2>Sub-processors</h2>
        <ul>
          <li>Microsoft Azure (hosting, compute, database) — US.</li>
          <li>Cloudflare (CDN, R2 object storage, email routing) — US.</li>
          <li>Auth0 / Okta (authentication) — US.</li>
          <li>Resend (transactional email) — US.</li>
          <li>Sentry / App Insights (error and uptime telemetry) — US.</li>
        </ul>
        <p>
          We notify customers at least 30 days before adding a new
          sub-processor.
        </p>
      </section>

      <section>
        <h2>Retention</h2>
        <ul>
          <li>Tenant operational data — retained for the contract term, then read-only export available for 30 days, then deleted.</li>
          <li>Audit log entries — retained 7 years per common SOC 2 evidence requirements, even after deletion of source rows.</li>
          <li>Backups — encrypted at rest, rolling 30-day retention.</li>
        </ul>
      </section>

      <section>
        <h2>International transfer</h2>
        <p>
          Where the customer is in the EU/EEA/UK and personal data is
          transferred to a US sub-processor, the Standard Contractual
          Clauses (2021) apply; the customer is the data exporter and the
          relevant sub-processor is the data importer.
        </p>
      </section>

      <div class="crosslinks">
        <a routerLink="/trust">Trust center</a>
        <a routerLink="/privacy">Privacy</a>
        <a routerLink="/terms">Terms</a>
        <a routerLink="/security">Security</a>
      </div>

      <p class="meta">Last reviewed: ${TRUST_LAST_REVIEWED}.</p>
    </main>
  `,
  styles: SHARED_STYLES,
})
export class DpaComponent {}
