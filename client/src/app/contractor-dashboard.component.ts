import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import {
  ContractorTimesheet,
  ContractorTimesheetService,
} from './core/contractor/contractor-timesheet.service';

interface DayRow {
  date: string;
  label: string;
  workHours: number | null;
  paidTimeOffHours: number | null;
}

@Component({
  selector: 'app-contractor-dashboard',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Contractor portal</p>
          <h1>Capture one clean week before approvals and billing take over.</h1>
          <p>
            This is the first live time-entry surface for Phase 2. Save drafts,
            submit the current week, and keep the contractor workflow attached
            to the same tenant-safe platform context already used by recruiting.
          </p>
        </div>
        <div class="hero-card">
          <p class="label">Week status</p>
          <strong>{{ statusLabel() }}</strong>
          <span>{{ statusDetail() }}</span>
          <dl class="hero-meta">
            <dt>Week start</dt>
            <dd>{{ weekStartUtc }}</dd>
            <dt>Total hours</dt>
            <dd>{{ totalEnteredHours() }}</dd>
            <dt>Regular / OT</dt>
            <dd>{{ totalsLabel() }}</dd>
          </dl>
        </div>
      </section>

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Contractor access starts with sign in.</h2>
          <p>
            Use the hosted Auth0 flow to enter the protected contractor area.
            Once signed in, this route becomes the weekly entry point for time
            capture and later client approval.
          </p>
          <div class="actions">
            <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
            <a routerLink="/jobs">Browse jobs first</a>
          </div>
        </section>
      } @else if (me.loading()) {
        <section class="gate-card">
          <h2>Hydrating your contractor session…</h2>
          <p>Waiting for the authenticated tenant claim before loading the weekly timesheet.</p>
        </section>
      } @else if (!tenantId()) {
        <section class="gate-card">
          <h2>Tenant assignment required.</h2>
          <p>
            Contractor time entry requires a <code>tenant_id</code> claim in
            the authenticated session. Re-sign in after the claim is assigned.
          </p>
        </section>
      } @else {
        <section class="workspace">
          <article class="timesheet-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Weekly entry</p>
                <h2>Current week draft</h2>
              </div>
              @if (loading()) {
                <span class="pill">Loading</span>
              } @else {
                <span class="pill">{{ timesheet()?.status || 'Draft' }}</span>
              }
            </div>

            @if (error()) {
              <p class="error">{{ error() }}</p>
            }

            @if (timesheet()?.reviewNote) {
              <p class="notice">
                Review note: {{ timesheet()?.reviewNote }}
              </p>
            }

            <form class="entry-grid" (ngSubmit)="saveDraft()">
              @for (row of rows(); track row.date) {
                <section class="day-card">
                  <div class="day-head">
                    <strong>{{ row.label }}</strong>
                    <span>{{ row.date }}</span>
                  </div>

                  <label>
                    Work hours
                    <input
                      type="number"
                      min="0"
                      max="24"
                      step="0.25"
                      inputmode="decimal"
                      [name]="'work-' + row.date"
                      [(ngModel)]="row.workHours"
                      [disabled]="!isEditable()"
                    />
                  </label>

                  <label>
                    PTO hours
                    <input
                      type="number"
                      min="0"
                      max="24"
                      step="0.25"
                      inputmode="decimal"
                      [name]="'pto-' + row.date"
                      [(ngModel)]="row.paidTimeOffHours"
                      [disabled]="!isEditable()"
                    />
                  </label>
                </section>
              }

              <div class="actions full-width">
                <button type="submit" class="primary" [disabled]="saving() || !isEditable()">
                  {{ saving() ? 'Saving…' : 'Save draft' }}
                </button>
                <button type="button" class="secondary" (click)="submitWeek()" [disabled]="submitting() || !isEditable()">
                  {{ submitting() ? 'Submitting…' : 'Submit week' }}
                </button>
                @if (flashMessage()) {
                  <span class="success">{{ flashMessage() }}</span>
                }
              </div>
            </form>
          </article>

          <aside class="workflow-card">
            <p class="eyebrow">Portal state</p>
            <h2>Time-entry workflow is now live.</h2>
            <dl class="meta-list">
              <dt>Email</dt>
              <dd>{{ auth.email() || 'Pending Auth0 profile' }}</dd>
              <dt>Tenant</dt>
              <dd>{{ tenantId() }}</dd>
              <dt>Status</dt>
              <dd>{{ timesheet()?.status || 'Draft' }}</dd>
              <dt>Next step</dt>
              <dd>Client approval flow</dd>
            </dl>
            <p>
              Drafts stay editable. Submitted weeks lock entry fields and set
              up the next Phase 2 slices for approval, pay rules, and invoice staging.
            </p>
            <p>
              Pay rules scaffold: {{ payRulesLabel() }}
            </p>
            <a routerLink="/recruiter">See recruiter side</a>
          </aside>
        </section>
      }
    </main>
  `,
  styles: `
    .page { width: min(1180px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero, .workspace { display: grid; gap: 1.25rem; }
    .hero { grid-template-columns: 1.1fr 0.9fr; margin-bottom: 1.5rem; }
    .workspace { grid-template-columns: minmax(0, 1.2fr) minmax(18rem, 0.8fr); align-items: start; }
    .hero-card, .gate-card, .timesheet-card, .workflow-card, .day-card {
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
    .hero-meta, .meta-list {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 0.45rem 0.9rem;
      margin: 1rem 0 0;
    }
    .hero-meta dt, .meta-list dt { font-weight: 700; color: #3f372c; }
    .hero-meta dd, .meta-list dd { margin: 0; color: #554d41; word-break: break-word; }
    .section-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1rem; }
    .pill {
      padding: 0.35rem 0.7rem;
      border-radius: 999px;
      background: #e7e5e4;
      color: #44403c;
      font-size: 0.85rem;
      font-weight: 700;
    }
    .entry-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 0.95rem;
    }
    .day-card { background: #fffdf9; }
    .day-head { display: flex; justify-content: space-between; gap: 0.8rem; align-items: baseline; margin-bottom: 0.8rem; }
    .day-head strong { color: #1f1d1a; }
    .day-head span { color: #6b6255; font-size: 0.92rem; }
    label { display: grid; gap: 0.35rem; color: #3f372c; font-weight: 600; margin-bottom: 0.7rem; }
    input {
      width: 100%;
      padding: 0.85rem 0.95rem;
      border-radius: 0.9rem;
      border: 1px solid #d8c8b0;
      background: #fffdf9;
      font: inherit;
    }
    .actions { display: flex; align-items: center; gap: 0.8rem; flex-wrap: wrap; }
    .full-width { grid-column: 1 / -1; }
    .primary, .secondary {
      padding: 0.9rem 1rem;
      border-radius: 999px;
      font-weight: 700;
      cursor: pointer;
    }
    .primary {
      border: 0;
      background: #1f2937;
      color: #fff8ee;
    }
    .secondary {
      border: 1px solid #d8c8b0;
      background: #fffdf9;
      color: #1f2937;
    }
    .notice {
      margin-bottom: 1rem;
      padding: 0.85rem 0.95rem;
      border-radius: 0.9rem;
      background: #fff3d8;
      color: #7c4a03;
      font-weight: 600;
    }
    .error { color: #b91c1c; font-weight: 600; }
    .success { color: #166534; font-weight: 600; }
    a { color: #9a3412; font-weight: 700; text-decoration: none; }
    @media (max-width: 980px) {
      .hero, .workspace, .entry-grid { grid-template-columns: 1fr; }
    }
  `,
})
export class ContractorDashboardComponent {
  protected auth = inject(AuthService);
  protected me = inject(MeService);
  private contractorTimesheet = inject(ContractorTimesheetService);

  protected readonly weekStartUtc = this.currentWeekStart();
  protected rows = signal<DayRow[]>(this.createEmptyRows(this.weekStartUtc));
  protected timesheet = signal<ContractorTimesheet | null>(null);
  protected loading = signal(false);
  protected saving = signal(false);
  protected submitting = signal(false);
  protected error = signal<string | null>(null);
  protected flashMessage = signal<string | null>(null);

  private loadedWeek = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (!this.auth.isAuthenticated() || this.me.loading()) {
        return;
      }

      if (!this.tenantId() || this.loadedWeek() === this.weekStartUtc) {
        return;
      }

      this.loadTimesheet();
    });
  }

  protected tenantId(): string | null {
    return this.me.data()?.tenantId ?? null;
  }

  protected isEditable(): boolean {
    const status = this.timesheet()?.status ?? 'Draft';
    return status === 'Draft' || status === 'Rejected';
  }

  protected statusLabel(): string {
    if (!this.auth.isAuthenticated()) {
      return 'Awaiting sign in';
    }

    if (this.me.loading() || this.loading()) {
      return 'Hydrating weekly entry';
    }

    if (!this.tenantId()) {
      return 'Authenticated without tenant context';
    }

    return this.timesheet()?.status
      ? `${this.timesheet()!.status} week`
      : 'Draft week ready';
  }

  protected statusDetail(): string {
    if (!this.auth.isAuthenticated()) {
      return 'Use Auth0 to enter the protected contractor surface.';
    }

    if (this.me.loading() || this.loading()) {
      return 'Waiting for the live /me payload and timesheet draft.';
    }

    if (!this.tenantId()) {
      return 'Re-sign in after tenant claims are assigned to your account.';
    }

    if (this.timesheet()?.status === 'Submitted') {
      return 'This week is locked and ready for the approval slice.';
    }

    return 'Drafts stay editable until you submit the week.';
  }

  protected totalEnteredHours(): number {
    return this.rows().reduce(
      (sum, row) => sum + (row.workHours ?? 0) + (row.paidTimeOffHours ?? 0),
      0,
    );
  }

  protected totalsLabel(): string {
    const totals = this.timesheet()?.totals;
    if (!totals) {
      return '0 / 0';
    }

    return `${totals.regularHours} reg · ${totals.overtimeHours} OT`;
  }

  protected payRulesLabel(): string {
    const totals = this.timesheet()?.totals;
    if (!totals) {
      return '0 work · 0 PTO · 0 payable';
    }

    return `${totals.workHours} work · ${totals.paidTimeOffHours} PTO · ${totals.payableHours} payable`;
  }

  protected saveDraft(): void {
    this.flashMessage.set(null);
    this.error.set(null);
    this.saving.set(true);

    this.contractorTimesheet.save(this.buildRequest()).subscribe({
      next: (timesheet) => {
        this.saving.set(false);
        this.applyTimesheet(timesheet);
        this.flashMessage.set('Draft saved.');
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(this.toErrorMessage(error));
      },
    });
  }

  protected submitWeek(): void {
    this.flashMessage.set(null);
    this.error.set(null);
    this.submitting.set(true);

    this.contractorTimesheet.submit(this.buildRequest()).subscribe({
      next: (timesheet) => {
        this.submitting.set(false);
        this.applyTimesheet(timesheet);
        this.flashMessage.set('Week submitted for approval.');
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.error.set(this.toErrorMessage(error));
      },
    });
  }

  private loadTimesheet(): void {
    this.loading.set(true);
    this.error.set(null);

    this.contractorTimesheet.current(this.weekStartUtc).subscribe({
      next: (timesheet) => {
        this.loading.set(false);
        this.loadedWeek.set(this.weekStartUtc);
        this.applyTimesheet(timesheet);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(this.toErrorMessage(error));
      },
    });
  }

  private applyTimesheet(timesheet: ContractorTimesheet): void {
    this.timesheet.set(timesheet);
    this.rows.set(this.createRowsFromTimesheet(timesheet));
  }

  private buildRequest() {
    return {
      weekStartUtc: this.weekStartUtc,
      entries: this.rows().flatMap((row) => {
        const entries = [];

        if ((row.workHours ?? 0) > 0) {
          entries.push({
            workDate: row.date,
            hours: row.workHours!,
            entryType: 'Work' as const,
            notes: null,
          });
        }

        if ((row.paidTimeOffHours ?? 0) > 0) {
          entries.push({
            workDate: row.date,
            hours: row.paidTimeOffHours!,
            entryType: 'PaidTimeOff' as const,
            notes: null,
          });
        }

        return entries;
      }),
    };
  }

  private createRowsFromTimesheet(timesheet: ContractorTimesheet): DayRow[] {
    const rows = this.createEmptyRows(timesheet.weekStartUtc);

    for (const entry of timesheet.entries) {
      const row = rows.find((item) => item.date === entry.workDate);
      if (!row) {
        continue;
      }

      if (entry.entryType === 'Work') {
        row.workHours = entry.hours;
      } else if (entry.entryType === 'PaidTimeOff') {
        row.paidTimeOffHours = entry.hours;
      }
    }

    return rows;
  }

  private createEmptyRows(weekStartUtc: string): DayRow[] {
    const start = new Date(`${weekStartUtc}T00:00:00`);

    return Array.from({ length: 7 }, (_, index) => {
      const date = new Date(start);
      date.setUTCDate(start.getUTCDate() + index);

      return {
        date: date.toISOString().slice(0, 10),
        label: date.toLocaleDateString('en-US', {
          weekday: 'short',
          month: 'short',
          day: 'numeric',
          timeZone: 'UTC',
        }),
        workHours: null,
        paidTimeOffHours: null,
      };
    });
  }

  private currentWeekStart(): string {
    const today = new Date();
    const day = today.getUTCDay();
    const offset = day === 0 ? -6 : 1 - day;
    today.setUTCDate(today.getUTCDate() + offset);
    return today.toISOString().slice(0, 10);
  }

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ?? error.error?.title ?? error.message;
    }

    return 'Unknown Error';
  }
}
