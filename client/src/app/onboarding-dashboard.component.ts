import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import {
  OnboardingDashboardService,
  OnboardingSummaryRow,
  OnboardingSummaryTotals,
} from './core/onboarding/onboarding-dashboard.service';

@Component({
  selector: 'app-onboarding-dashboard',
  imports: [],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Onboarding</p>
          <h1>Track every consultant's onboarding to done.</h1>
          <p>
            A live view of checklist completion across your bench — who's ready,
            who's mid-flight, and who hasn't started — so nothing stalls before a
            consultant can be billed.
          </p>
        </div>
        <div class="hero-card">
          <p class="label">Overall completion</p>
          <strong>{{ totals()?.overallCompletionPercent ?? 0 }}%</strong>
          <span>{{ totals()?.consultants ?? 0 }} consultants tracked</span>
        </div>
      </section>

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Sign in to view onboarding progress.</h2>
          <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
        </section>
      } @else if (!hasAccess()) {
        <section class="gate-card">
          <h2>Recruiting access required.</h2>
          <p>This dashboard needs recruiting or admin access.</p>
        </section>
      } @else {
        <section class="stat-row">
          <article class="stat-card">
            <span class="stat-num">{{ totals()?.consultants ?? 0 }}</span>
            <span class="stat-label">Consultants</span>
          </article>
          <article class="stat-card">
            <span class="stat-num">{{ totals()?.complete ?? 0 }}</span>
            <span class="stat-label">Fully onboarded</span>
          </article>
          <article class="stat-card">
            <span class="stat-num">{{ totals()?.inProgress ?? 0 }}</span>
            <span class="stat-label">In progress</span>
          </article>
        </section>

        <section class="board-card">
          <div class="section-head">
            <div>
              <p class="eyebrow">By consultant</p>
              <h2>Completion progress</h2>
            </div>
            @if (loading()) {
              <span class="pill">Loading</span>
            }
          </div>

          @if (error()) {
            <p class="error" role="alert" aria-live="assertive">{{ error() }}</p>
          } @else {
            <div class="rows">
              @for (row of consultants(); track row.candidateProfileId) {
                <article class="row">
                  <div class="row-head">
                    <div>
                      <strong>{{ row.name }}</strong>
                      <p>{{ row.email }}</p>
                    </div>
                    <span class="status" [class]="statusClass(row.status)">{{ row.status }}</span>
                  </div>

                  <div class="bar" [attr.aria-label]="row.completionPercent + '% complete'">
                    <div class="bar-fill" [style.width.%]="row.completionPercent"></div>
                  </div>

                  <div class="row-meta">
                    <span>{{ row.completionPercent }}%</span>
                    <span>{{ row.completed }} done · {{ row.inReview }} in review · {{ row.pending }} pending</span>
                  </div>
                </article>
              } @empty {
                <article class="empty-card">
                  <h3>No onboarding items yet</h3>
                  <p>Assign checklist items to a consultant and their progress appears here.</p>
                </article>
              }
            </div>
          }
        </section>
      }
    </main>
  `,
  styles: `
    .page { width: min(1180px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero { display: grid; grid-template-columns: 1.15fr 0.85fr; gap: 1.25rem; margin-bottom: 1.5rem; }
    .hero-card, .gate-card, .stat-card, .board-card, .row, .empty-card {
      padding: 1.6rem;
      border-radius: 1.5rem;
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
      box-shadow: var(--shadow-md);
    }
    .eyebrow { margin: 0 0 0.7rem; color: var(--color-primary, #1a3a8f); text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.8rem; font-size: clamp(2.1rem, 3.8vw, 4.2rem); line-height: 0.98; }
    h2, h3 { margin: 0; }
    p { color: var(--color-ink-muted, #4b5a72); line-height: 1.7; }
    .label { margin: 0 0 0.4rem; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; color: var(--color-primary, #1a3a8f); font-weight: 800; }
    .hero-card strong { display: block; font-size: 2.4rem; line-height: 1; margin-bottom: 0.35rem; color: var(--color-primary, #1a3a8f); }
    .stat-row { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1.25rem; margin-bottom: 1.5rem; }
    .stat-card { display: grid; gap: 0.3rem; text-align: center; }
    .stat-num { font-size: 2.2rem; font-weight: 800; color: var(--color-primary, #1a3a8f); }
    .stat-label { color: var(--color-ink-muted, #4b5a72); font-weight: 600; }
    .section-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1rem; }
    .pill { padding: 0.35rem 0.7rem; border-radius: 999px; background: var(--color-surface-alt, #f0f3f9); color: var(--color-ink, #1a2942); font-size: 0.85rem; font-weight: 700; }
    .rows { display: grid; gap: 0.9rem; }
    .row-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 0.7rem; }
    .row-head strong { font-size: 1.05rem; }
    .status { padding: 0.3rem 0.7rem; border-radius: 999px; font-size: 0.8rem; font-weight: 700; white-space: nowrap; }
    .status--complete { background: #dcfce7; color: #166534; }
    .status--review { background: #dbeafe; color: #1e40af; }
    .status--progress { background: var(--color-surface-alt, #f0f3f9); color: #475569; }
    .bar { height: 10px; border-radius: 999px; background: var(--color-surface-alt, #eef2f9); overflow: hidden; }
    .bar-fill { height: 100%; border-radius: 999px; background: linear-gradient(90deg, #1e5fd0, #1733a6); transition: width 0.3s ease; }
    .row-meta { display: flex; justify-content: space-between; gap: 1rem; margin-top: 0.55rem; color: var(--color-ink-muted, #4b5a72); font-weight: 600; font-size: 0.92rem; }
    .primary { padding: 0.9rem 1rem; border-radius: 999px; font-weight: 700; cursor: pointer; border: 0; background: var(--color-primary, #1a3a8f); color: #ffffff; }
    .error { color: #b91c1c; font-weight: 600; }
    @media (max-width: 980px) {
      .hero, .stat-row { grid-template-columns: 1fr; }
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

  protected statusClass(status: string): string {
    if (status === 'Complete') return 'status--complete';
    if (status === 'In review') return 'status--review';
    return 'status--progress';
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
