import { Component, computed, inject, signal } from '@angular/core';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import {
  BgcCheck,
  BgcDashboardResponse,
  BgcService,
  BgcStatus,
  BgcWebhookEvent,
  BgcRenewal,
} from './core/bgc/bgc.service';
import {
  QaBadgeComponent,
  QaButtonComponent,
  QaCardComponent,
  QaPageHeadComponent,
  QaStatComponent,
} from './core/ui';

@Component({
  selector: 'app-bgc-dashboard',
  imports: [QaBadgeComponent, QaButtonComponent, QaCardComponent, QaPageHeadComponent, QaStatComponent],
  template: `
    <main class="page">
      <qa-page-head
        eyebrow="Screening"
        title="Background checks"
        lede="Order, monitor, and act on consultant screenings — Checkr integrated."
      >
        <qa-button variant="ghost">Export CSV</qa-button>
        <qa-button variant="primary" (click)="onRequest()">Request check</qa-button>
      </qa-page-head>

      @if (!auth.isAuthenticated()) {
        <qa-card>
          <h2>Sign in to view screening.</h2>
          <p>Recruiting access required.</p>
          <qa-button variant="primary" (click)="auth.loginWithRedirect()">Sign in</qa-button>
        </qa-card>
      } @else if (!hasAccess()) {
        <qa-card>
          <h2>Recruiting access required.</h2>
        </qa-card>
      } @else if (data(); as d) {
        <section class="stat-row">
          <qa-stat label="Cleared" [value]="d.totals.cleared" detail="last 30 days" />
          <qa-stat label="In progress" [value]="d.totals.inProgress" detail="avg 3.2 days" />
          <qa-stat label="Flagged" [value]="d.totals.flagged" detail="needs review" />
          <qa-stat label="Awaiting auth" [value]="d.totals.awaitingAuth" detail="consultant must consent" />
        </section>

        <section class="grid-2">
          <qa-card>
            <header class="sec-head">
              <h2>Active checks</h2>
            </header>
            <table class="t">
              <thead>
                <tr>
                  <th>Consultant</th>
                  <th>Package</th>
                  <th>Requested</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (c of d.checks; track c.id) {
                  <tr>
                    <td>
                      <strong>{{ c.consultantName }}</strong>
                      <p class="muted">{{ c.consultantEmail }}</p>
                    </td>
                    <td>{{ c.package }}</td>
                    <td>{{ c.requestedAtUtc }}</td>
                    <td><qa-badge [tone]="badgeTone(c.status)">{{ statusLabel(c) }}</qa-badge></td>
                    <td class="right">
                      @if (c.status === 'Flagged') {
                        <qa-button variant="primary" (click)="onReview(c)">Review</qa-button>
                      } @else if (c.status === 'Cleared') {
                        <qa-button variant="ghost" (click)="onView(c)">View report</qa-button>
                      } @else if (c.status === 'AwaitingAuth') {
                        <qa-button variant="ghost" (click)="onResend(c)">Resend</qa-button>
                      } @else {
                        <qa-button variant="ghost" (click)="onView(c)">Open</qa-button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </qa-card>

          <qa-card>
            <header class="sec-head">
              <h2>Recent Checkr events</h2>
              <qa-badge tone="info">Live webhook</qa-badge>
            </header>
            @for (e of d.events; track e.id) {
              <div class="event">
                <div>
                  <strong>{{ e.type }}</strong>
                  <p class="muted">{{ e.subject }} · {{ e.detail }}</p>
                </div>
                <qa-badge [tone]="eventTone(e)">{{ e.receivedAtUtc }}</qa-badge>
              </div>
            }
          </qa-card>
        </section>

        <qa-card>
          <header class="sec-head">
            <div>
              <h2>Compliance &amp; renewals</h2>
              <p class="muted">Re-screen anyone whose last clear is older than the tenant policy (12 months).</p>
            </div>
          </header>
          @for (r of d.renewals; track r.id) {
            <div class="renewal">
              <div>
                <strong>{{ r.consultantName }}</strong>
                <p class="muted">Last cleared {{ r.lastClearedOnUtc }} · {{ r.ageLabel }}</p>
              </div>
              <qa-badge [tone]="renewalTone(r)">{{ renewalLabel(r) }}</qa-badge>
              @if (r.status === 'due') {
                <qa-button variant="ghost" (click)="onRenew(r)">Renew</qa-button>
              } @else {
                <span></span>
              }
            </div>
          }
        </qa-card>
      } @else {
        <qa-card><p>Loading…</p></qa-card>
      }

      @if (toast()) {
        <div class="toast" role="status" aria-live="polite">{{ toast() }}</div>
      }
    </main>
  `,
  styles: `
    .page { width: min(1180px, 100%); margin: 0 auto; padding: 1.75rem 0 3rem; }

    .stat-row {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 1.1rem;
      margin-bottom: 1.1rem;
    }
    .grid-2 {
      display: grid;
      grid-template-columns: 1.4fr 0.9fr;
      gap: 1.1rem;
      align-items: start;
      margin-bottom: 1.1rem;
    }
    qa-card { display: block; }
    .sec-head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      margin-bottom: 0.6rem;
    }
    .sec-head h2 { margin: 0; font-size: 1.05rem; font-weight: 700; color: var(--color-ink-strong, #0d1b2a); }

    table.t { width: 100%; border-collapse: collapse; font-size: 0.92rem; }
    table.t th {
      text-align: left;
      padding: 0.55rem 0.55rem;
      color: var(--color-ink-muted, #4b5a72);
      font-weight: 700;
      font-size: 0.74rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      border-bottom: 1px solid var(--color-border, #d8dee9);
    }
    table.t td { padding: 0.7rem 0.55rem; border-top: 1px solid var(--color-border, #d8dee9); vertical-align: top; }
    table.t td.right { text-align: right; white-space: nowrap; }
    table.t tr:hover td { background: #fafbff; }
    table.t strong { display: block; color: var(--color-ink-strong, #0d1b2a); }

    .muted { margin: 0; color: var(--color-ink-muted, #4b5a72); font-size: 0.82rem; }

    .event, .renewal {
      display: grid;
      grid-template-columns: 1fr auto auto;
      gap: 0.85rem;
      align-items: center;
      padding: 0.7rem 0;
      border-top: 1px solid var(--color-border, #d8dee9);
    }
    .event { grid-template-columns: 1fr auto; }
    .event:first-of-type, .renewal:first-of-type { border-top: 0; }
    .event strong, .renewal strong { display: block; color: var(--color-ink-strong, #0d1b2a); }

    .toast {
      position: fixed;
      left: 50%;
      bottom: 1.5rem;
      transform: translateX(-50%);
      background: var(--color-ink-strong, #0d1b2a);
      color: #fff;
      padding: 0.7rem 1.1rem;
      border-radius: 999px;
      font-weight: 600;
      box-shadow: var(--shadow-md, 0 4px 6px rgba(13,27,42,.08));
      z-index: 50;
    }

    @media (max-width: 980px) {
      .stat-row { grid-template-columns: repeat(2, 1fr); }
      .grid-2 { grid-template-columns: 1fr; }
    }
  `,
})
export class BgcDashboardComponent {
  protected auth = inject(AuthService);
  protected access = inject(AccessService);
  private service = inject(BgcService);

  protected data = signal<BgcDashboardResponse | null>(null);
  protected toast = signal<string | null>(null);
  protected hasAccess = computed(() => this.access.canAccessRecruitingWorkspace());

  constructor() {
    if (this.hasAccess()) {
      this.service.list().subscribe({
        next: (r) => this.data.set(r),
        error: () => this.data.set(null),
      });
    }
  }

  protected badgeTone(s: BgcStatus): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    if (s === 'Cleared') return 'success';
    if (s === 'InProgress') return 'warning';
    if (s === 'Flagged') return 'danger';
    if (s === 'AwaitingAuth') return 'info';
    return 'neutral';
  }
  protected statusLabel(c: BgcCheck): string {
    if (c.status === 'Cleared' && c.completedAtUtc) return `Cleared · ${c.completedAtUtc}`;
    if (c.status === 'InProgress') return 'In progress';
    if (c.status === 'AwaitingAuth') return 'Awaiting authorization';
    return c.status;
  }

  protected eventTone(e: BgcWebhookEvent): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    if (e.severity === 'ok') return 'success';
    if (e.severity === 'warning') return 'warning';
    if (e.severity === 'danger') return 'danger';
    if (e.severity === 'review') return 'info';
    return 'neutral';
  }

  protected renewalTone(r: BgcRenewal): 'success' | 'warning' | 'neutral' {
    if (r.status === 'current') return 'success';
    if (r.status === 'due') return 'warning';
    return 'neutral';
  }
  protected renewalLabel(r: BgcRenewal): string {
    if (r.status === 'current') return 'Current';
    if (r.status === 'due') return 'Re-screen due';
    return 'Renews next month';
  }

  protected onRequest(): void { this.fireToast('Order check form opens here'); }
  protected onReview(c: BgcCheck): void { this.fireToast(`Adverse-action review for ${c.consultantName}`); }
  protected onView(c: BgcCheck): void { this.fireToast(`Opening ${c.consultantName}'s check`); }
  protected onResend(c: BgcCheck): void { this.fireToast(`Reminder sent to ${c.consultantName}`); }
  protected onRenew(r: BgcRenewal): void { this.fireToast(`Renewal queued for ${r.consultantName}`); }

  private fireToast(msg: string): void {
    this.toast.set(msg);
    setTimeout(() => this.toast.set(null), 2200);
  }
}
