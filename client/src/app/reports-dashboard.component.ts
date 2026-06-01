import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import {
  BurnRateResponse,
  BurnRateWeek,
  ReportingService,
} from './core/reporting/reporting.service';
import {
  QaButtonComponent,
  QaCardComponent,
  QaPageHeadComponent,
  QaStatComponent,
} from './core/ui';

@Component({
  selector: 'app-reports-dashboard',
  imports: [QaButtonComponent, QaCardComponent, QaPageHeadComponent, QaStatComponent],
  template: `
    <main class="page">
      <qa-page-head
        eyebrow="Reports"
        title="Project burn rate"
        lede="Payable hours logged per week across your consultants, with an estimated cost based on your default rate."
      >
        @for (w of windows; track w) {
          <qa-button [variant]="weeks() === w ? 'primary' : 'ghost'" (click)="setWeeks(w)">{{ w }}w</qa-button>
        }
      </qa-page-head>

      @if (!auth.isAuthenticated()) {
        <qa-card>
          <h2 class="card-title">Sign in to view reports.</h2>
          <qa-button variant="primary" (click)="auth.loginWithRedirect()">Sign in</qa-button>
        </qa-card>
      } @else if (!hasAccess()) {
        <qa-card><h2 class="card-title">Recruiting access required.</h2></qa-card>
      } @else if (error()) {
        <qa-card><p class="error">{{ error() }}</p></qa-card>
      } @else {
        <section class="stat-row">
          <qa-stat
            label="Total payable hours"
            [value]="data()?.totalPayableHours ?? 0"
            [detail]="'last ' + weeks() + ' weeks'"
          />
          <qa-stat
            label="Consultants"
            [value]="data()?.consultants?.length ?? 0"
            detail="contributing"
          />
          <qa-stat
            label="Est. cost"
            [value]="(data()?.currency ?? 'USD') + ' ' + ((data()?.estimatedCost ?? 0).toLocaleString())"
            detail="@ default rate"
          />
        </section>

        <qa-card>
          <header class="sec-head"><h2 class="card-title">Weekly burn</h2></header>
          @if (data()?.weeks?.length) {
            <div class="chart">
              @for (week of data()!.weeks; track week.weekStartUtc) {
                <div class="bar-col" [title]="week.weekStartUtc + ' · ' + week.payableHours + 'h'">
                  <div class="bar" [style.height.%]="barHeight(week)"></div>
                  <span class="bar-val">{{ week.payableHours }}</span>
                  <span class="bar-label">{{ week.weekStartUtc.slice(5) }}</span>
                </div>
              }
            </div>
          } @else {
            <p class="muted">No submitted or approved timesheets in this window yet.</p>
          }
        </qa-card>

        <qa-card>
          <header class="sec-head"><h2 class="card-title">By consultant</h2></header>
          <div class="rows">
            @for (c of data()?.consultants ?? []; track c.consultant) {
              <div class="row">
                <span>{{ c.consultant }}</span>
                <strong>{{ c.payableHours }}h</strong>
              </div>
            } @empty {
              <p class="muted">No consultant hours yet.</p>
            }
          </div>
        </qa-card>
      }
    </main>
  `,
  styles: `
    .page { width: min(1100px, 100%); margin: 0 auto; padding: 1.75rem 0 3rem; }
    qa-page-head { display: block; }
    qa-page-head ::ng-deep .head__actions qa-button { display: inline-block; }

    .stat-row {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1.1rem;
      margin-bottom: 1.1rem;
    }
    qa-card { display: block; margin-bottom: 1.1rem; }
    .sec-head { margin-bottom: 0.85rem; }
    .card-title { margin: 0; font-size: 1.1rem; color: var(--color-ink-strong, #0d1b2a); }

    .chart {
      display: flex;
      align-items: flex-end;
      gap: 0.6rem;
      height: 220px;
      padding-top: 1rem;
      overflow-x: auto;
    }
    .bar-col {
      display: flex; flex-direction: column;
      align-items: center; justify-content: flex-end;
      flex: 1 0 2.5rem; height: 100%;
    }
    .bar {
      width: 60%; min-height: 2px;
      border-radius: 6px 6px 0 0;
      background: linear-gradient(180deg, #1e5fd0, #1733a6);
    }
    .bar-val { font-size: 0.75rem; font-weight: 700; margin-top: 0.3rem; color: var(--color-ink, #1a2942); }
    .bar-label { font-size: 0.7rem; color: var(--color-ink-muted, #4b5a72); }

    .rows { display: grid; gap: 0.5rem; }
    .row {
      display: flex; justify-content: space-between;
      padding: 0.65rem 0.85rem; border-radius: 0.55rem;
      background: var(--color-surface-alt, #f0f3f9);
    }
    .muted { color: var(--color-ink-muted, #4b5a72); }
    .error { color: var(--color-danger, #b02a37); font-weight: 600; }

    @media (max-width: 820px) {
      .stat-row { grid-template-columns: 1fr; }
    }
  `,
})
export class ReportsDashboardComponent {
  protected auth = inject(AuthService);
  protected access = inject(AccessService);
  private service = inject(ReportingService);

  protected data = signal<BurnRateResponse | null>(null);
  protected error = signal<string | null>(null);
  protected weeks = signal(8);
  protected readonly windows = [4, 8, 12, 26];

  protected hasAccess = computed(() => this.access.canAccessRecruitingWorkspace());

  private peakHours = computed(() =>
    Math.max(1, ...(this.data()?.weeks ?? []).map((w) => w.payableHours)),
  );

  constructor() {
    this.load();
  }

  protected setWeeks(weeks: number): void {
    this.weeks.set(weeks);
    this.load();
  }

  protected barHeight(week: BurnRateWeek): number {
    return Math.round((week.payableHours / this.peakHours()) * 100);
  }

  private load(): void {
    if (!this.hasAccess()) return;
    this.error.set(null);
    this.service.burnRate(this.weeks()).subscribe({
      next: (r) => this.data.set(r),
      error: (e: unknown) => {
        this.error.set(
          e instanceof HttpErrorResponse ? (e.error?.detail ?? e.error?.title ?? e.message) : 'Error',
        );
      },
    });
  }
}
