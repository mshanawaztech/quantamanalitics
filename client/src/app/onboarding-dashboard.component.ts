import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import {
  OnboardingDashboardService,
  OnboardingSummaryRow,
  OnboardingSummaryTotals,
} from './core/onboarding/onboarding-dashboard.service';
import {
  QaBadgeComponent,
  QaButtonComponent,
  QaCardComponent,
  QaPageHeadComponent,
  QaProgressComponent,
  QaStatComponent,
} from './core/ui';

@Component({
  selector: 'app-onboarding-dashboard',
  imports: [
    QaBadgeComponent,
    QaButtonComponent,
    QaCardComponent,
    QaPageHeadComponent,
    QaProgressComponent,
    QaStatComponent,
  ],
  template: `
    <main class="page">
      <qa-page-head
        eyebrow="Onboarding · management"
        title="Track every consultant's onboarding to done."
        lede="A live view of checklist completion across your bench — who's ready, who's mid-flight, and who hasn't started — so nothing stalls before a consultant can be billed."
      >
        <qa-button variant="primary">Apply template</qa-button>
      </qa-page-head>

      @if (!auth.isAuthenticated()) {
        <qa-card>
          <h2 class="card-title">Sign in to view onboarding progress.</h2>
          <qa-button variant="primary" (click)="auth.loginWithRedirect()">Sign in</qa-button>
        </qa-card>
      } @else if (!hasAccess()) {
        <qa-card>
          <h2 class="card-title">Recruiting access required.</h2>
          <p class="muted">This dashboard needs recruiting or admin access.</p>
        </qa-card>
      } @else {
        <section class="stat-row">
          <qa-stat label="Consultants" [value]="totals()?.consultants ?? 0" detail="tracked" />
          <qa-stat label="Fully onboarded" [value]="totals()?.complete ?? 0" detail="complete" />
          <qa-stat label="In progress" [value]="totals()?.inProgress ?? 0" detail="action needed" />
          <qa-stat label="Overall" [value]="(totals()?.overallCompletionPercent ?? 0) + '%'" detail="avg completion" />
        </section>

        <qa-card>
          <header class="sec-head">
            <div>
              <p class="eyebrow">By consultant</p>
              <h2 class="card-title">Completion progress</h2>
            </div>
            @if (loading()) {
              <qa-badge tone="neutral">Loading…</qa-badge>
            }
          </header>

          @if (error()) {
            <p class="error" role="alert" aria-live="assertive">{{ error() }}</p>
          } @else {
            <div class="rows">
              @for (row of consultants(); track row.candidateProfileId) {
                <article class="row">
                  <div class="row__head">
                    <div>
                      <strong>{{ row.name }}</strong>
                      <p class="muted">{{ row.email }}</p>
                    </div>
                    <qa-badge [tone]="badgeTone(row.status)">{{ row.status }}</qa-badge>
                  </div>
                  <qa-progress [value]="row.completionPercent" [ariaLabel]="row.name + ' ' + row.completionPercent + '% complete'" />
                  <div class="row__meta">
                    <span>{{ row.completionPercent }}%</span>
                    <span>{{ row.completed }} done · {{ row.inReview }} in review · {{ row.pending }} pending</span>
                  </div>
                </article>
              } @empty {
                <article class="empty">
                  <h3>No onboarding items yet</h3>
                  <p class="muted">Assign checklist items to a consultant and their progress appears here.</p>
                </article>
              }
            </div>
          }
        </qa-card>
      }
    </main>
  `,
  styles: `
    .page { width: min(1180px, 100%); margin: 0 auto; padding: 1.75rem 0 3rem; }

    .stat-row {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 1.1rem;
      margin-bottom: 1.25rem;
    }
    qa-card { display: block; margin-bottom: 1.1rem; }

    .sec-head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      margin-bottom: 0.85rem;
    }
    .eyebrow {
      margin: 0 0 0.25rem;
      font-size: 0.74rem;
      font-weight: 800;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--color-primary, #1a3a8f);
    }
    .card-title { margin: 0; font-size: 1.1rem; color: var(--color-ink-strong, #0d1b2a); }
    .muted { margin: 0.15rem 0 0; color: var(--color-ink-muted, #4b5a72); font-size: 0.88rem; }

    .rows { display: grid; gap: 0.7rem; }
    .row {
      padding: 0.95rem 1.1rem;
      border: 1px solid var(--color-border, #d8dee9);
      border-radius: 0.75rem;
      background: var(--color-surface, #fff);
    }
    .row__head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      margin-bottom: 0.6rem;
    }
    .row__head strong { display: block; color: var(--color-ink-strong, #0d1b2a); }
    .row__meta {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      margin-top: 0.55rem;
      color: var(--color-ink-muted, #4b5a72);
      font-weight: 600;
      font-size: 0.9rem;
    }

    .empty {
      padding: 1.5rem;
      border: 1px dashed var(--color-border, #d8dee9);
      border-radius: 0.75rem;
      text-align: center;
    }
    .error { color: var(--color-danger, #b02a37); font-weight: 600; }

    @media (max-width: 980px) {
      .stat-row { grid-template-columns: repeat(2, 1fr); }
    }
  `,
})
export class OnboardingDashboardComponent {
  protected auth = inject(AuthService);
  protected access = inject(AccessService);
  private service = inject(OnboardingDashboardService);

  protected consultants = signal<OnboardingSummaryRow[]>([]);
  protected totals = signal<OnboardingSummaryTotals | null>(null);
  protected loading = signal(false);
  protected error = signal<string | null>(null);

  protected hasAccess = computed(() => this.access.canAccessRecruitingWorkspace());

  constructor() {
    effect(() => {
      if (!this.hasAccess()) {
        return;
      }
      this.load();
    });
  }

  protected badgeTone(status: string): 'success' | 'info' | 'warning' | 'neutral' {
    if (status === 'Complete') return 'success';
    if (status === 'In review') return 'info';
    if (status === 'In progress') return 'warning';
    return 'neutral';
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.service.summary().subscribe({
      next: (response) => {
        this.loading.set(false);
        this.consultants.set(response.consultants);
        this.totals.set(response.totals);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(
          error instanceof HttpErrorResponse
            ? (error.error?.detail ?? error.error?.title ?? error.message)
            : 'Unknown Error',
        );
      },
    });
  }
}
