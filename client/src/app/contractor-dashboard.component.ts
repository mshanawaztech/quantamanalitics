import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';

@Component({
  selector: 'app-contractor-dashboard',
  imports: [RouterLink],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Contractor portal</p>
          <h1>Track weekly time before approvals and invoicing kick in.</h1>
          <p>
            This shell makes the Phase 2 direction visible: a protected home
            for time entry, submission state, and invoice-adjacent status once
            the next slices land.
          </p>
        </div>
        <div class="hero-card">
          <p class="label">Portal status</p>
          <strong>{{ statusLabel() }}</strong>
          <span>{{ statusDetail() }}</span>
        </div>
      </section>

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Contractor access starts with sign in.</h2>
          <p>
            Use the hosted Auth0 flow to enter the protected contractor area.
            Once signed in, this route becomes the launch point for weekly
            timesheets and approval tracking.
          </p>
          <div class="actions">
            <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
            <a routerLink="/jobs">Browse jobs first</a>
          </div>
        </section>
      } @else {
        <section class="workspace">
          <article class="status-card">
            <p class="eyebrow">Access signal</p>
            <h2>Live tenant-aware session</h2>

            @if (me.loading()) {
              <p class="pill-row"><span class="pill">Loading</span></p>
            } @else if (me.error()) {
              <p class="error">{{ me.error() }}</p>
            }

            <dl class="meta-list">
              <dt>Email</dt>
              <dd>{{ auth.email() || 'Pending Auth0 profile' }}</dd>
              <dt>Tenant</dt>
              <dd>{{ me.data()?.tenantId || 'Pending tenant claim' }}</dd>
              <dt>Roles</dt>
              <dd>{{ roleLabel() }}</dd>
              <dt>Next deliverable</dt>
              <dd>Weekly time entry</dd>
            </dl>
          </article>

          <article class="timeline-card">
            <p class="eyebrow">Workflow map</p>
            <h2>How this contractor surface will grow</h2>
            <div class="steps">
              <section class="step">
                <strong>1. Weekly entry</strong>
                <p>Fill a week with dated work and PTO lines inside the current tenant.</p>
              </section>
              <section class="step">
                <strong>2. Submission</strong>
                <p>Lock the week, send it forward, and keep a clear submitted/returned state.</p>
              </section>
              <section class="step">
                <strong>3. Approval + billing handoff</strong>
                <p>Client-side approval and invoice staging will sit on the same timesheet record.</p>
              </section>
            </div>
          </article>
        </section>

        <section class="preview-card">
          <div class="section-head">
            <div>
              <p class="eyebrow">Phase 2 baseline</p>
              <h2>Timesheet domain is live underneath this shell.</h2>
            </div>
            <a routerLink="/recruiter">See recruiter side</a>
          </div>

          <div class="preview-grid">
            <article>
              <strong>Weekly aggregate</strong>
              <p>The backend now has a tenant-scoped <code>Timesheet</code> aggregate with draft, submitted, approved, and rejected states.</p>
            </article>
            <article>
              <strong>Dated entries</strong>
              <p><code>TimeEntry</code> rows are modeled separately so later PRs can add daily work, PTO, and approval-safe totals without reshaping persistence.</p>
            </article>
            <article>
              <strong>What lands next</strong>
              <p>The next UI slice will connect this page to real weekly entry forms instead of placeholder roadmap copy.</p>
            </article>
          </div>
        </section>
      }
    </main>
  `,
  styles: `
    .page { width: min(1120px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero, .workspace { display: grid; gap: 1.25rem; }
    .hero { grid-template-columns: 1.15fr 0.85fr; margin-bottom: 1.5rem; }
    .workspace { grid-template-columns: 0.9fr 1.1fr; align-items: start; margin-bottom: 1.25rem; }
    .hero-card, .gate-card, .status-card, .timeline-card, .preview-card, .step, .preview-grid article {
      padding: 1.6rem;
      border-radius: 1.5rem;
      background: rgb(255 251 244 / 0.88);
      border: 1px solid rgb(87 70 42 / 0.14);
      box-shadow: 0 1rem 2rem rgb(64 47 22 / 0.06);
    }
    .eyebrow { margin: 0 0 0.7rem; color: #9a3412; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.8rem; font-size: clamp(2.1rem, 3.8vw, 4.2rem); line-height: 0.98; }
    h2 { margin: 0; font-size: 1.4rem; }
    p { color: #554d41; line-height: 1.7; }
    .label { margin: 0 0 0.4rem; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; color: #9a3412; font-weight: 800; }
    .hero-card strong { display: block; font-size: 1.2rem; margin-bottom: 0.45rem; }
    .hero-card span { color: #6b6255; }
    .actions, .section-head { display: flex; align-items: center; gap: 0.8rem; flex-wrap: wrap; }
    .section-head { justify-content: space-between; margin-bottom: 1rem; }
    .primary {
      padding: 0.9rem 1rem;
      border: 0;
      border-radius: 999px;
      background: #1f2937;
      color: #fff8ee;
      font-weight: 700;
      cursor: pointer;
    }
    a { color: #9a3412; font-weight: 700; text-decoration: none; }
    .pill-row { margin: 0 0 1rem; }
    .pill {
      padding: 0.35rem 0.7rem;
      border-radius: 999px;
      background: #e7e5e4;
      color: #44403c;
      font-size: 0.85rem;
      font-weight: 700;
    }
    .meta-list { display: grid; grid-template-columns: auto 1fr; gap: 0.45rem 0.9rem; margin: 1rem 0 0; }
    .meta-list dt { font-weight: 700; color: #3f372c; }
    .meta-list dd { margin: 0; color: #554d41; word-break: break-word; }
    .steps, .preview-grid { display: grid; gap: 0.9rem; }
    .step, .preview-grid article { background: #fffdf9; border: 1px solid #eadcc8; }
    .step strong, .preview-grid strong { display: block; margin-bottom: 0.55rem; font-size: 1.02rem; color: #1f1d1a; }
    .error { color: #b91c1c; font-weight: 600; }
    @media (max-width: 900px) {
      .hero, .workspace { grid-template-columns: 1fr; }
    }
  `,
})
export class ContractorDashboardComponent {
  protected auth = inject(AuthService);
  protected me = inject(MeService);

  protected statusLabel(): string {
    if (!this.auth.isAuthenticated()) {
      return 'Awaiting sign in';
    }

    if (this.me.loading()) {
      return 'Hydrating contractor session';
    }

    if (this.me.error()) {
      return 'Authenticated with API mismatch';
    }

    return this.me.data()?.tenantId
      ? 'Contractor shell ready'
      : 'Authenticated without tenant context';
  }

  protected statusDetail(): string {
    if (!this.auth.isAuthenticated()) {
      return 'Use Auth0 to enter the protected contractor surface.';
    }

    if (this.me.loading()) {
      return 'Waiting for the live /me payload and tenant claim.';
    }

    if (this.me.error()) {
      return this.me.error() ?? 'Unknown /me error';
    }

    return this.me.data()?.tenantId
      ? 'The next slice will attach weekly entry to this live tenant-aware session.'
      : 'Sign in again once tenant claims are assigned for your account.';
  }

  protected roleLabel(): string {
    return this.me.data()?.roles.join(', ') || 'No roles returned yet';
  }
}
