import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ContractorInvoicesService,
  CreateInvoiceRequest,
  InvoiceResponse,
} from './core/contractor/invoices.service';
import {
  QaAlertComponent,
  QaButtonComponent,
  QaEmptyStateComponent,
  QaInputComponent,
} from './core/ui';

/**
 * Track-B closeout — contractor-side invoice surface. Lets a direct
 * contractor draft + submit invoices. The backend (POST /api/v1/contractor
 * /invoices, /submit transition) shipped in Phase 6 Story 48 but never had
 * a UI consumer in the contractor dashboard.
 *
 * Layout: left = the user's recent invoices, right = a draft-an-invoice
 * form. Both stack on mobile. Status pill per row so a contractor can
 * see at-a-glance what's in flight vs paid.
 */
@Component({
  selector: 'app-contractor-invoices',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    QaAlertComponent,
    QaButtonComponent,
    QaEmptyStateComponent,
    QaInputComponent,
  ],
  template: `
    <main class="page">
      <header class="page__head">
        <p class="eyebrow">Contractor</p>
        <h1>Invoices</h1>
        <p class="lede">
          Submit a direct invoice for hours billed outside the timesheet
          flow. Drafts stay editable until you submit; submitted invoices
          go to the client portal for approval, then platform-admin marks
          them paid.
        </p>
      </header>

      @if (loadError()) {
        <qa-alert tone="danger" role="alert">{{ loadError() }}</qa-alert>
      }

      <div class="layout">
        <!-- ── Existing invoices ──────────────────────────────────── -->
        <section class="rail" aria-labelledby="rail-heading">
          <header>
            <h2 id="rail-heading">Recent invoices</h2>
            <button type="button" class="rail__refresh" (click)="refresh()">Refresh</button>
          </header>

          @if (loading()) {
            <p class="hint">Loading…</p>
          } @else if (invoices().length === 0) {
            <qa-empty-state
              title="No invoices yet"
              hint="Use the form to draft your first invoice."
            />
          } @else {
            <ul class="rows">
              @for (inv of invoices(); track inv.id) {
                <li class="row">
                  <div class="row__head">
                    <span class="row__period">
                      {{ inv.periodStartUtc }} → {{ inv.periodEndUtc }}
                    </span>
                    <span class="row__status row__status--{{ inv.status.toLowerCase() }}">
                      {{ inv.status }}
                    </span>
                  </div>
                  <div class="row__amount">
                    <strong>{{ inv.currency }} {{ inv.amount.toFixed(2) }}</strong>
                    <small>{{ inv.hours }} hr</small>
                  </div>
                  @if (inv.notes) {
                    <p class="row__notes">{{ inv.notes }}</p>
                  }
                  @if (inv.reviewerNote) {
                    <p class="row__reviewer">Reviewer: "{{ inv.reviewerNote }}"</p>
                  }
                  @if (inv.status === 'Draft') {
                    <div class="row__actions">
                      <qa-button
                        variant="primary"
                        [disabled]="submittingId() === inv.id"
                        (click)="submit(inv)"
                      >{{ submittingId() === inv.id ? 'Submitting…' : 'Submit for review' }}</qa-button>
                    </div>
                  }
                </li>
              }
            </ul>
          }
        </section>

        <!-- ── Draft form ─────────────────────────────────────────── -->
        <section class="form" aria-labelledby="form-heading">
          <header>
            <h2 id="form-heading">Draft a new invoice</h2>
            <p>Lands as a Draft. You can keep editing in this list until you submit.</p>
          </header>

          @if (createSuccess()) {
            <qa-alert tone="success" role="status">Draft saved.</qa-alert>
          }
          @if (createError()) {
            <qa-alert tone="danger" role="alert">{{ createError() }}</qa-alert>
          }

          <form (submit)="onCreate($event)">
            <div class="grid">
              <qa-input
                label="Period start"
                type="date"
                [(ngModel)]="draftPeriodStart"
                name="periodStart"
                [required]="true"
              ></qa-input>
              <qa-input
                label="Period end"
                type="date"
                [(ngModel)]="draftPeriodEnd"
                name="periodEnd"
                [required]="true"
              ></qa-input>
              <qa-input
                label="Hours"
                type="number"
                [(ngModel)]="draftHours"
                name="hours"
                hint="Total hours invoiced. Must be ≥ 0."
                [required]="true"
              ></qa-input>
              <qa-input
                label="Hourly rate"
                type="number"
                [(ngModel)]="draftHourlyRate"
                name="hourlyRate"
                hint="Rate per hour in the currency below."
                [required]="true"
              ></qa-input>
              <div class="field">
                <label class="field__label" for="invoice-currency">Currency <span aria-hidden="true">*</span></label>
                <select
                  id="invoice-currency"
                  class="field__select"
                  [(ngModel)]="draftCurrency"
                  name="currency"
                  required
                >
                  @for (code of supportedCurrencies; track code) {
                    <option [value]="code">{{ code }}</option>
                  }
                </select>
                <p class="field__hint">Pick the billing currency. USD is the default.</p>
              </div>
              <div class="field field--readonly">
                <label class="field__label">Total amount</label>
                <output class="field__amount">{{ draftCurrency }} {{ computedAmountDisplay() }}</output>
                <p class="field__hint">Auto-calculated as Hours × Hourly rate.</p>
              </div>
            </div>

            <label class="notes-label" for="invoice-notes">Notes (optional)</label>
            <textarea
              id="invoice-notes"
              rows="4"
              [(ngModel)]="draftNotes"
              name="notes"
              placeholder="PO number, project name, anything the client needs to see."
            ></textarea>

            <div class="form__actions">
              <qa-button
                variant="primary"
                [disabled]="creating()"
                type="submit"
              >{{ creating() ? 'Saving…' : 'Save draft' }}</qa-button>
            </div>
          </form>
        </section>
      </div>
    </main>
  `,
  styles: `
    :host { display: block; }
    .page { width: min(1240px, 100%); margin: 0 auto; padding: 2rem 1.5rem 4rem; }
    .eyebrow { color: var(--color-primary, #1a3a8f); font-size: 12px; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; margin: 0 0 0.5rem; }
    .page__head { margin-bottom: 1.5rem; }
    h1 { margin: 0 0 0.5rem; font-size: 1.75rem; }
    .lede { color: var(--color-fg-muted, #5d6577); margin: 0; max-width: 60ch; }

    .layout {
      display: grid;
      grid-template-columns: 1.1fr 0.9fr;
      gap: 1.5rem;
      align-items: start;
    }
    @media (max-width: 960px) { .layout { grid-template-columns: 1fr; } }

    section.rail, section.form {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 12px;
      padding: 1.25rem;
    }
    section header {
      display: flex; align-items: baseline; justify-content: space-between;
      gap: 1rem; flex-wrap: wrap; margin-bottom: 1rem;
    }
    section h2 { margin: 0; font-size: 1.1rem; }
    section header p { margin: 0; color: var(--color-fg-muted, #5d6577); font-size: 0.875rem; }
    .rail__refresh {
      background: none; border: 0; color: var(--color-primary, #1a3a8f);
      font-size: 0.85rem; font-weight: 600; cursor: pointer; padding: 0.25rem 0.5rem;
      border-radius: 6px;
    }
    .rail__refresh:hover { background: var(--color-primary-soft, #e7ecf6); }

    .rows { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.625rem; }
    .row {
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 10px;
      padding: 0.875rem;
      display: grid;
      gap: 0.5rem;
    }
    .row__head { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
    .row__period { font-size: 0.85rem; color: var(--color-fg-muted, #5d6577); }
    .row__status {
      padding: 0.15rem 0.5rem;
      border-radius: 999px;
      font-size: 0.7rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }
    .row__status--draft { background: #f1f5f9; color: #475569; }
    .row__status--submitted { background: var(--color-primary-soft, #e7ecf6); color: var(--color-primary, #1a3a8f); }
    .row__status--approved { background: #dcfce7; color: #166534; }
    .row__status--rejected { background: #fee2e2; color: #991b1b; }
    .row__status--paid { background: #ecfeff; color: #155e75; }

    .row__amount { display: flex; align-items: baseline; gap: 0.5rem; }
    .row__amount strong { font-size: 1.05rem; }
    .row__amount small { color: var(--color-fg-muted, #5d6577); font-size: 0.85rem; }
    .row__notes { margin: 0; color: var(--color-fg-muted, #5d6577); font-size: 0.875rem; line-height: 1.4; }
    .row__reviewer { margin: 0; color: #475569; font-size: 0.85rem; font-style: italic; }
    .row__actions { display: flex; gap: 0.5rem; justify-content: flex-end; }

    .grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.875rem;
      margin-bottom: 0.875rem;
    }
    @media (max-width: 540px) { .grid { grid-template-columns: 1fr; } }

    .field { display: flex; flex-direction: column; gap: 0.375rem; }
    .field__label { font-size: 0.85rem; font-weight: 600; color: var(--color-fg, #1a1f2c); }
    .field__label [aria-hidden="true"] { color: #c0392b; margin-left: 0.15rem; }
    .field__select {
      width: 100%;
      font-family: inherit;
      font-size: 0.95rem;
      padding: 0.5rem 0.625rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 8px;
      background: var(--color-surface, #fff);
    }
    .field__select:focus-visible {
      outline: 3px solid var(--color-primary, #1a3a8f);
      outline-offset: 1px;
    }
    .field__hint { margin: 0; color: var(--color-fg-muted, #5d6577); font-size: 0.8rem; }
    .field--readonly .field__amount {
      display: inline-block;
      padding: 0.5rem 0.75rem;
      border: 1px dashed var(--color-border, #d8dde7);
      border-radius: 8px;
      background: var(--color-primary-soft, #e7ecf6);
      color: var(--color-primary, #1a3a8f);
      font-weight: 700;
      font-size: 1.05rem;
    }

    .notes-label { display: block; font-size: 0.85rem; font-weight: 600; margin-bottom: 0.375rem; }
    textarea {
      width: 100%;
      font-family: inherit;
      font-size: 0.95rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 8px;
      padding: 0.625rem;
      resize: vertical;
    }
    textarea:focus-visible {
      outline: 3px solid var(--color-primary, #1a3a8f);
      outline-offset: 1px;
    }
    .form__actions { display: flex; justify-content: flex-end; margin-top: 1rem; }

    .hint { color: var(--color-fg-muted, #5d6577); }
  `,
})
export class ContractorInvoicesComponent {
  private svc = inject(ContractorInvoicesService);

  protected readonly invoices = signal<InvoiceResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly submittingId = signal<string | null>(null);

  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);
  protected readonly createSuccess = signal(false);

  // Form fields — plain strings because qa-input wraps a primitive input.
  protected draftPeriodStart = '';
  protected draftPeriodEnd = '';
  protected draftHours = '';
  protected draftHourlyRate = '';
  protected draftCurrency = 'USD';
  protected draftNotes = '';

  /** Currencies offered in the dropdown. USD first so it's the default. */
  protected readonly supportedCurrencies: readonly string[] = [
    'USD',
    'EUR',
    'GBP',
    'CAD',
    'AUD',
    'INR',
  ];

  /**
   * Computed total = Hours × Hourly rate. Stored as a signal-free getter so
   * Angular's OnPush change detection picks it up when the bound inputs
   * change (ngModel triggers CD). Negative / NaN inputs collapse to 0 so
   * the field never displays junk while the user is mid-typing.
   */
  protected readonly computedAmount = (): number => {
    const hours = Number(this.draftHours);
    const rate = Number(this.draftHourlyRate);
    if (!Number.isFinite(hours) || !Number.isFinite(rate)) return 0;
    if (hours < 0 || rate < 0) return 0;
    return hours * rate;
  };

  protected readonly computedAmountDisplay = (): string =>
    this.computedAmount().toFixed(2);

  protected readonly hasInvoices = computed(() => this.invoices().length > 0);

  constructor() {
    this.refresh();
    this.resetForm();
  }

  protected refresh(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.svc.list().subscribe({
      next: (r) => {
        this.invoices.set(r.items);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.loadError.set(this.toMessage(e));
        this.loading.set(false);
      },
    });
  }

  protected onCreate(event: Event): void {
    event.preventDefault();
    this.creating.set(true);
    this.createError.set(null);
    this.createSuccess.set(false);

    const hours = Number(this.draftHours);
    const rate = Number(this.draftHourlyRate);
    if (!Number.isFinite(hours) || hours < 0) {
      this.createError.set('Hours must be a non-negative number.');
      this.creating.set(false);
      return;
    }
    if (!Number.isFinite(rate) || rate < 0) {
      this.createError.set('Hourly rate must be a non-negative number.');
      this.creating.set(false);
      return;
    }

    const request: CreateInvoiceRequest = {
      periodStartUtc: this.draftPeriodStart,
      periodEndUtc: this.draftPeriodEnd,
      hours,
      // Round to 2 decimal places so the persisted amount matches the
      // total the contractor sees on screen.
      amount: Math.round(hours * rate * 100) / 100,
      currency: this.draftCurrency.trim().toUpperCase(),
      notes: this.draftNotes.trim() || null,
    };

    this.svc.create(request).subscribe({
      next: (created) => {
        this.invoices.update((list) => [created, ...list]);
        this.createSuccess.set(true);
        this.creating.set(false);
        this.resetForm();
        setTimeout(() => this.createSuccess.set(false), 3000);
      },
      error: (e: unknown) => {
        this.createError.set(this.toMessage(e));
        this.creating.set(false);
      },
    });
  }

  protected submit(invoice: InvoiceResponse): void {
    this.submittingId.set(invoice.id);

    this.svc.submit(invoice.id).subscribe({
      next: (updated) => {
        this.invoices.update((list) =>
          list.map((inv) => (inv.id === updated.id ? updated : inv)),
        );
        this.submittingId.set(null);
      },
      error: (e: unknown) => {
        this.submittingId.set(null);
        this.loadError.set(this.toMessage(e));
      },
    });
  }

  private resetForm(): void {
    const today = new Date().toISOString().slice(0, 10);
    this.draftPeriodStart = today;
    this.draftPeriodEnd = today;
    this.draftHours = '0';
    this.draftHourlyRate = '0';
    this.draftCurrency = 'USD';
    this.draftNotes = '';
  }

  private toMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as { detail?: string; title?: string } | null;
      return body?.detail ?? body?.title ?? error.message;
    }
    return error instanceof Error ? error.message : 'Unexpected error.';
  }
}
