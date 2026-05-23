import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import {
  BurnRateResponse,
  BurnRateWeek,
  ReportingService,
} from './core/reporting/reporting.service';

@Component({
  selector: 'app-reports-dashboard',
  imports: [],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Reports</p>
          <h1>Project burn rate</h1>
          <p>Payable hours logged per week across your consultants, with an estimated cost based on your default rate.</p>
        </div>
        <div class="hero-card">
          <p class="label">Total payable hours</p>
          <strong>{{ data()?.totalPayableHours ?? 0 }}</strong>
          <span>est. {{ data()?.currency }} {{ (data()?.estimatedCost ?? 0).toFixed(2) }}</span>
        </div>
      </section>

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Sign in to view reports.</h2>
          <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
        </section>
      } @else if (!hasAccess()) {
        <section class="gate-card"><h2>Recruiting access required.</h2></section>
      } @else if (error()) {
        <section class="gate-card"><p class="error">{{ error() }}</p></section>
      } @else {
        <section class="card">
          <div class="card-head">
            <h2>Weekly burn</h2>
            <div class="range">
              @for (w of windows; track w) {
                <button type="button" [class.active]="weeks() === w" (click)="setWeeks(w)">{{ w }}w</button>
              }
            </div>
          </div>

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
            <p class="empty">No submitted or approved timesheets in this window yet.</p>
          }
        </section>

        <section class="card">
          <h2>By consultant</h2>
          <div class="rows">
            @for (c of data()?.consultants ?? []; track c.consultant) {
              <div class="row">
                <span>{{ c.consultant }}</span>
                <strong>{{ c.payableHours }}h</strong>
              </div>
            } @empty {
              <p class="empty">No consultant hours yet.</p>
            }
          </div>
        </section>
      }
    </main>
  `,
  styles: `
    .page { width: min(1000px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero { display: grid; grid-template-columns: 1.15fr 0.85fr; gap: 1.25rem; margin-bottom: 1.5rem; }
    .hero-card, .gate-card, .card {
      padding: 1.5rem; border-radius: 1.25rem;
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
      box-shadow: var(--shadow-md);
    }
    .card { margin-bottom: 1.25rem; }
    .eyebrow { margin: 0 0 0.6rem; color: var(--color-primary, #1a3a8f); text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.6rem; font-size: clamp(1.9rem, 3.4vw, 3.2rem); line-height: 1.02; }
    h2 { margin: 0; font-size: 1.2rem; }
    p { color: var(--color-ink-muted, #4b5a72); line-height: 1.6; margin: 0; }
    .label { margin: 0 0 0.35rem; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; color: var(--color-primary, #1a3a8f); font-weight: 800; }
    .hero-card strong { display: block; font-size: 2.4rem; line-height: 1; margin-bottom: 0.3rem; color: var(--color-primary, #1a3a8f); }
    .card-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.2rem; }
    .range button { border: 1px solid var(--color-border, #d8dee9); background: #fff; border-radius: 999px; padding: 0.35rem 0.7rem; margin-left: 0.4rem; cursor: pointer; font-weight: 600; font-size: 0.85rem; }
    .range button.active { background: var(--color-primary, #1a3a8f); color: #fff; border-color: var(--color-primary, #1a3a8f); }
    .chart { display: flex; align-items: flex-end; gap: 0.6rem; height: 220px; padding-top: 1rem; overflow-x: auto; }
    .bar-col { display: flex; flex-direction: column; align-items: center; justify-content: flex-end; flex: 1 0 2.5rem; height: 100%; }
    .bar { width: 60%; min-height: 2px; border-radius: 6px 6px 0 0; background: linear-gradient(180deg, #1e5fd0, #1733a6); }
    .bar-val { font-size: 0.75rem; font-weight: 700; margin-top: 0.3rem; color: var(--color-ink, #1a2942); }
    .bar-label { font-size: 0.7rem; color: var(--color-ink-muted, #4b5a72); }
    .rows { display: grid; gap: 0.5rem; }
    .row { display: flex; justify-content: space-between; padding: 0.6rem 0.8rem; border-radius: 0.7rem; background: var(--color-surface-alt, #f0f3f9); }
    .empty { color: var(--color-ink-muted, #4b5a72); }
    .error { color: #b91c1c; font-weight: 600; }
    .primary { padding: 0.85rem 1.1rem; border-radius: 999px; border: 0; background: var(--color-primary, #1a3a8f); color: #fff; font-weight: 700; cursor: pointer; }
    @media (max-width: 820px) { .hero { grid-template-columns: 1fr; } }
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
