import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ContractorInvoicesService,
  CreateInvoiceRequest,
  InvoiceLineItemRequest,
  InvoiceResponse,
  InvoiceStatus,
} from './core/contractor/invoices.service';
import {
  QaAlertComponent,
  QaButtonComponent,
  QaEmptyStateComponent,
  QaInputComponent,
} from './core/ui';

/**
 * Contractor invoices — v2.
 *
 * Three-pane layout matching the Quantam Analytics invoices mockup:
 *  - Left rail: paginated list of the contractor's invoices, status filter
 *    on top, status pill per card. Clicking a Draft card loads it into the
 *    middle form for editing; clicking any non-draft card shows it read-only.
 *  - Middle: form with sections (details, client, work period, invoice items
 *    table). Each line item self-computes its amount; an item button adds a
 *    row, the trash icon removes one. At least one row is required.
 *  - Right: summary card with subtotal / tax line / total, currency dropdown,
 *    optional notes (500 char limit), and Save draft / Save and send actions.
 *
 * State:
 *  - `selectedInvoiceId` = null → "new invoice" mode (POST on save)
 *  - `selectedInvoiceId` = id    → editing an existing Draft (PUT on save).
 *    Non-draft selections render the form read-only — the contractor can
 *    review the historical record but cannot mutate it.
 */
@Component({
  selector: 'app-contractor-invoices',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    QaAlertComponent,
    QaButtonComponent,
    QaEmptyStateComponent,
    QaInputComponent,
  ],
  template: `
    <main class="page">
      <header class="page__head">
        <div class="page__head-left">
          <h1>Invoices</h1>
          <p class="lede">Create, manage and track your invoices</p>
        </div>
        <div class="page__head-actions">
          <qa-button variant="ghost" [disabled]="true">Download report</qa-button>
          <qa-button variant="primary" (click)="startNewInvoice()">+ New invoice</qa-button>
        </div>
      </header>

      @if (loadError()) {
        <qa-alert tone="danger" role="alert">{{ loadError() }}</qa-alert>
      }
      @if (saveSuccess()) {
        <qa-alert tone="success" role="status">{{ saveSuccess() }}</qa-alert>
      }
      @if (formError()) {
        <qa-alert tone="danger" role="alert">{{ formError() }}</qa-alert>
      }

      <form class="layout" (submit)="$event.preventDefault()">
        <!-- ─────────────── Left rail ─────────────── -->
        <section class="rail" aria-labelledby="rail-heading">
          <header class="rail__head">
            <h2 id="rail-heading">Your invoices</h2>
            <select
              class="rail__filter"
              [(ngModel)]="statusFilter"
              (ngModelChange)="onFilterChange()"
              name="statusFilter"
              aria-label="Filter by status"
            >
              <option value="All">All statuses</option>
              <option value="Draft">Draft</option>
              <option value="Submitted">Submitted</option>
              <option value="Approved">Approved</option>
              <option value="Rejected">Rejected</option>
              <option value="Paid">Paid</option>
            </select>
          </header>

          @if (loading()) {
            <p class="hint">Loading…</p>
          } @else if (filteredInvoices().length === 0) {
            <qa-empty-state
              title="No invoices yet"
              description="Use the form to draft your first invoice."
            />
          } @else {
            <ul class="rows">
              @for (inv of pagedInvoices(); track inv.id) {
                <li>
                  <button
                    type="button"
                    class="row"
                    [class.row--selected]="selectedInvoiceId() === inv.id"
                    (click)="onSelectInvoice(inv)"
                    [attr.aria-pressed]="selectedInvoiceId() === inv.id"
                  >
                    <div class="row__head">
                      <span class="row__number">{{ inv.invoiceNumber || '—' }}</span>
                      <span class="row__status row__status--{{ inv.status.toLowerCase() }}">
                        {{ inv.status }}
                      </span>
                    </div>
                    <div class="row__body">
                      <div class="row__client">{{ inv.clientName || 'Unspecified client' }}</div>
                      <div class="row__amount">
                        <strong>{{ inv.currency }} {{ inv.amount | number:'1.2-2' }}</strong>
                      </div>
                    </div>
                    <div class="row__foot">
                      <span class="row__period">
                        {{ inv.periodStartUtc }} → {{ inv.periodEndUtc }}
                      </span>
                      <span class="row__detail">{{ statusDetailFor(inv) }}</span>
                    </div>
                  </button>
                </li>
              }
            </ul>

            @if (totalPages() > 1) {
              <nav class="rail__pager" aria-label="Pagination">
                <span class="rail__pager-summary">
                  Showing {{ pagedInvoices().length }} of {{ filteredInvoices().length }} invoices
                </span>
                <div class="rail__pager-controls">
                  <button
                    type="button"
                    (click)="goToPage(page() - 1)"
                    [disabled]="page() === 1"
                    aria-label="Previous page"
                  >‹</button>
                  @for (n of pageNumbers(); track n) {
                    <button
                      type="button"
                      class="rail__pager-num"
                      [class.rail__pager-num--active]="n === page()"
                      (click)="goToPage(n)"
                    >{{ n }}</button>
                  }
                  <button
                    type="button"
                    (click)="goToPage(page() + 1)"
                    [disabled]="page() === totalPages()"
                    aria-label="Next page"
                  >›</button>
                </div>
              </nav>
            }
          }
        </section>

        <!-- ─────────────── Middle form ─────────────── -->
        <section class="form" aria-labelledby="form-heading">
          <header class="form__head">
            <div>
              <h2 id="form-heading">{{ isEditing() ? 'Edit invoice' : 'Create new invoice' }}</h2>
              <p>
                {{ isReadOnly()
                  ? 'This invoice is locked because it has been ' + selectedInvoice()?.status?.toLowerCase() + '.'
                  : 'Enter the details below to generate your invoice.' }}
              </p>
            </div>
            @if (selectedInvoice()) {
              <qa-button variant="ghost" (click)="startNewInvoice()">Cancel</qa-button>
            }
          </header>

          <fieldset class="section" [disabled]="isReadOnly()">
            <legend>Invoice details</legend>
            <div class="grid grid--3">
              <div class="field">
                <label class="field__label">Invoice number</label>
                <div class="field__locked">
                  <span>{{ formInvoiceNumber() || 'Auto-generated on save' }}</span>
                </div>
                <p class="field__hint">Auto-numbered per tenant per year.</p>
              </div>
              <qa-input
                label="Issue date"
                type="date"
                [(ngModel)]="formIssueDate"
                name="issueDate"
                [required]="true"
              ></qa-input>
              <qa-input
                label="Due date"
                type="date"
                [(ngModel)]="formDueDate"
                name="dueDate"
                [required]="true"
              ></qa-input>
            </div>
          </fieldset>

          <fieldset class="section" [disabled]="isReadOnly()">
            <legend>Client</legend>
            <qa-input
              label="Client name"
              [(ngModel)]="formClientName"
              name="clientName"
              hint="The company you are billing. A Client picker is coming in v3."
            ></qa-input>
          </fieldset>

          <fieldset class="section" [disabled]="isReadOnly()">
            <legend>Work period</legend>
            <div class="grid grid--2">
              <qa-input
                label="Period start"
                type="date"
                [(ngModel)]="formPeriodStart"
                name="periodStart"
                [required]="true"
              ></qa-input>
              <qa-input
                label="Period end"
                type="date"
                [(ngModel)]="formPeriodEnd"
                name="periodEnd"
                [required]="true"
              ></qa-input>
            </div>
          </fieldset>

          <fieldset class="section" [disabled]="isReadOnly()">
            <legend>Invoice items</legend>
            <div class="items">
              <div class="items__head">
                <span class="items__col items__col--description">Description</span>
                <span class="items__col items__col--hours">Hours</span>
                <span class="items__col items__col--rate">Rate</span>
                <span class="items__col items__col--amount">Amount</span>
                <span class="items__col items__col--actions" aria-hidden="true"></span>
              </div>
              @for (item of formLineItems(); track $index; let i = $index) {
                <div class="items__row">
                  <input
                    class="items__input items__col--description"
                    type="text"
                    [ngModel]="item.description"
                    (ngModelChange)="updateLineItem(i, 'description', $event)"
                    name="li-desc-{{ i }}"
                    placeholder="Consulting Services"
                  />
                  <input
                    class="items__input items__col--hours"
                    type="number"
                    step="0.01"
                    min="0"
                    [ngModel]="item.hours"
                    (ngModelChange)="updateLineItem(i, 'hours', $event)"
                    name="li-hours-{{ i }}"
                    aria-label="Hours"
                  />
                  <input
                    class="items__input items__col--rate"
                    type="number"
                    step="0.01"
                    min="0"
                    [ngModel]="item.rate"
                    (ngModelChange)="updateLineItem(i, 'rate', $event)"
                    name="li-rate-{{ i }}"
                    aria-label="Rate"
                  />
                  <span class="items__amount items__col--amount">
                    {{ formCurrency }} {{ lineItemAmount(item) | number:'1.2-2' }}
                  </span>
                  <button
                    type="button"
                    class="items__delete items__col--actions"
                    (click)="removeLineItem(i)"
                    [disabled]="formLineItems().length === 1"
                    aria-label="Delete line item"
                  >×</button>
                </div>
              }
              <button
                type="button"
                class="items__add"
                (click)="addLineItem()"
              >+ Add item</button>
            </div>
          </fieldset>
        </section>

        <!-- ─────────────── Right summary card ─────────────── -->
        <aside class="summary" aria-labelledby="summary-heading">
          <h2 id="summary-heading">Summary</h2>

          <dl class="summary__totals">
            <div>
              <dt>Subtotal</dt>
              <dd>{{ formCurrency }} {{ formSubtotal() | number:'1.2-2' }}</dd>
            </div>
            <div>
              <dt>
                <span>Tax</span>
                <input
                  class="summary__tax-input"
                  type="number"
                  min="0"
                  max="100"
                  step="0.01"
                  [(ngModel)]="formTaxRate"
                  name="taxRate"
                  [disabled]="isReadOnly()"
                  aria-label="Tax rate (%)"
                /><span>%</span>
              </dt>
              <dd>{{ formCurrency }} {{ formTaxAmount() | number:'1.2-2' }}</dd>
            </div>
            <div class="summary__total">
              <dt>Total</dt>
              <dd>{{ formCurrency }} {{ formTotal() | number:'1.2-2' }} {{ formCurrency }}</dd>
            </div>
          </dl>

          <div class="field">
            <label class="field__label" for="invoice-currency">Currency</label>
            <select
              id="invoice-currency"
              class="field__select"
              [(ngModel)]="formCurrency"
              name="currency"
              [disabled]="isReadOnly()"
            >
              @for (c of supportedCurrencies; track c.code) {
                <option [value]="c.code">{{ c.code }} – {{ c.label }}</option>
              }
            </select>
            <p class="field__hint">All amounts shown in {{ formCurrency }}.</p>
          </div>

          <div class="field">
            <label class="field__label" for="invoice-notes">Notes (optional)</label>
            <textarea
              id="invoice-notes"
              rows="4"
              [(ngModel)]="formNotes"
              name="notes"
              maxlength="500"
              [disabled]="isReadOnly()"
              placeholder="Add any notes or payment instructions for your client…"
            ></textarea>
            <p class="field__counter">{{ formNotes.length }}/500</p>
          </div>

          @if (!isReadOnly()) {
            <div class="summary__actions">
              <qa-button
                variant="primary"
                [disabled]="saving() || sending()"
                (click)="onSave(false)"
              >{{ saving() ? 'Saving…' : 'Save draft' }}</qa-button>
              <qa-button
                variant="ghost"
                [disabled]="saving() || sending()"
                (click)="onSave(true)"
              >{{ sending() ? 'Sending…' : 'Save and send' }}</qa-button>
            </div>
          } @else if (selectedInvoice()?.status === 'Draft') {
            <div class="summary__actions">
              <qa-button
                variant="primary"
                [disabled]="sending()"
                (click)="submitSelected()"
              >{{ sending() ? 'Submitting…' : 'Submit for review' }}</qa-button>
            </div>
          }

          @if (selectedInvoice()) {
            <div class="summary__actions summary__actions--pdf">
              <qa-button
                variant="ghost"
                [disabled]="downloadingPdf()"
                (click)="downloadPdf()"
              >{{ downloadingPdf() ? 'Generating PDF…' : 'Download PDF' }}</qa-button>
            </div>
          }
        </aside>
      </form>
    </main>
  `,
  styles: `
    :host { display: block; }
    .page { width: min(1480px, 100%); margin: 0 auto; padding: 1.5rem 1.5rem 4rem; }

    /* ── Header ──────────────────────────────────────────────────── */
    .page__head {
      display: flex; align-items: flex-start; justify-content: space-between;
      gap: 1rem; flex-wrap: wrap; margin-bottom: 1.5rem;
    }
    .page__head-left h1 { margin: 0 0 0.25rem; font-size: 1.625rem; }
    .lede { color: var(--color-fg-muted, #5d6577); margin: 0; }
    .page__head-actions { display: flex; gap: 0.625rem; flex-wrap: wrap; }

    /* ── Layout ──────────────────────────────────────────────────── */
    .layout {
      display: grid;
      grid-template-columns: minmax(280px, 360px) minmax(0, 1fr) minmax(260px, 320px);
      gap: 1.25rem;
      align-items: start;
    }
    @media (max-width: 1200px) { .layout { grid-template-columns: 1fr 1fr; } .summary { grid-column: 1 / -1; } }
    @media (max-width: 800px)  { .layout { grid-template-columns: 1fr; } .summary { grid-column: auto; } }

    /* ── Card shell ──────────────────────────────────────────────── */
    section.rail, section.form, aside.summary {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 14px;
      padding: 1.25rem;
    }

    /* ── Rail ────────────────────────────────────────────────────── */
    .rail__head {
      display: flex; align-items: center; justify-content: space-between;
      gap: 0.75rem; margin-bottom: 1rem; flex-wrap: wrap;
    }
    .rail h2 { margin: 0; font-size: 1.05rem; }
    .rail__filter {
      font-family: inherit; font-size: 0.85rem;
      padding: 0.4rem 0.625rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 8px;
      background: var(--color-surface, #fff);
    }
    .rows { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.75rem; }
    .row {
      width: 100%;
      display: grid; gap: 0.5rem;
      padding: 0.875rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 10px;
      background: var(--color-surface, #fff);
      text-align: left;
      cursor: pointer;
      transition: border-color 0.12s, background 0.12s;
    }
    .row:hover { border-color: var(--color-primary, #1a3a8f); }
    .row--selected {
      border-color: var(--color-primary, #1a3a8f);
      background: var(--color-primary-soft, #e7ecf6);
    }
    .row__head { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; }
    .row__number { font-weight: 700; font-size: 0.95rem; }
    .row__status {
      padding: 0.15rem 0.55rem; border-radius: 999px;
      font-size: 0.7rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em;
    }
    .row__status--draft { background: #f1f5f9; color: #475569; }
    .row__status--submitted { background: var(--color-primary-soft, #e7ecf6); color: var(--color-primary, #1a3a8f); }
    .row__status--approved { background: #dcfce7; color: #166534; }
    .row__status--rejected { background: #fee2e2; color: #991b1b; }
    .row__status--paid { background: #ecfeff; color: #155e75; }
    .row__body { display: flex; justify-content: space-between; align-items: baseline; gap: 0.5rem; }
    .row__client { font-size: 0.9rem; color: var(--color-fg, #1a1f2c); }
    .row__amount strong { font-size: 1rem; }
    .row__foot { display: flex; justify-content: space-between; gap: 0.5rem; font-size: 0.78rem; color: var(--color-fg-muted, #5d6577); }

    .rail__pager { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; margin-top: 1rem; flex-wrap: wrap; }
    .rail__pager-summary { font-size: 0.8rem; color: var(--color-fg-muted, #5d6577); }
    .rail__pager-controls { display: flex; gap: 0.25rem; }
    .rail__pager-controls button {
      min-width: 2rem; height: 2rem;
      border: 1px solid var(--color-border, #d8dde7); border-radius: 8px;
      background: var(--color-surface, #fff); cursor: pointer;
      font-family: inherit; font-size: 0.85rem;
    }
    .rail__pager-controls button:disabled { opacity: 0.4; cursor: not-allowed; }
    .rail__pager-num--active {
      background: var(--color-primary, #1a3a8f) !important;
      color: #fff; border-color: var(--color-primary, #1a3a8f);
    }

    /* ── Form ────────────────────────────────────────────────────── */
    .form__head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; margin-bottom: 1.25rem; flex-wrap: wrap; }
    .form h2 { margin: 0 0 0.25rem; font-size: 1.1rem; }
    .form__head p { margin: 0; color: var(--color-fg-muted, #5d6577); font-size: 0.875rem; }
    .section { border: 0; padding: 0; margin: 0 0 1.25rem; }
    .section legend { font-size: 0.95rem; font-weight: 700; margin-bottom: 0.75rem; padding: 0; }
    .grid { display: grid; gap: 0.75rem; }
    .grid--2 { grid-template-columns: 1fr 1fr; }
    .grid--3 { grid-template-columns: 1fr 1fr 1fr; }
    @media (max-width: 700px) { .grid--3, .grid--2 { grid-template-columns: 1fr; } }

    /* ── Items table ─────────────────────────────────────────────── */
    .items {
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 10px;
      overflow: hidden;
    }
    .items__head, .items__row {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 90px 110px 130px 36px;
      gap: 0.5rem;
      padding: 0.625rem 0.75rem;
      align-items: center;
    }
    .items__head {
      background: var(--color-bg-muted, #f4f6fa);
      font-size: 0.8rem; font-weight: 700;
      color: var(--color-fg-muted, #5d6577);
      text-transform: uppercase; letter-spacing: 0.05em;
    }
    .items__row { border-top: 1px solid var(--color-border, #d8dde7); }
    .items__input {
      width: 100%;
      font-family: inherit; font-size: 0.9rem;
      padding: 0.45rem 0.55rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 7px;
      background: var(--color-surface, #fff);
    }
    .items__input:focus-visible {
      outline: 3px solid var(--color-primary, #1a3a8f);
      outline-offset: 1px;
    }
    .items__amount { font-weight: 600; }
    .items__delete {
      background: #fee2e2; border: 1px solid #fecaca; color: #991b1b;
      width: 30px; height: 30px; border-radius: 8px;
      cursor: pointer; font-size: 1rem; line-height: 1;
    }
    .items__delete:disabled { opacity: 0.35; cursor: not-allowed; }
    .items__add {
      display: block; width: 100%;
      padding: 0.75rem; text-align: center;
      background: var(--color-surface, #fff); border: 0; border-top: 1px dashed var(--color-border, #d8dde7);
      color: var(--color-primary, #1a3a8f); font-weight: 600; font-family: inherit; font-size: 0.9rem;
      cursor: pointer;
    }
    .items__add:hover { background: var(--color-primary-soft, #e7ecf6); }
    @media (max-width: 700px) {
      .items__head { display: none; }
      .items__row { grid-template-columns: 1fr; }
      .items__delete { justify-self: end; }
    }

    /* ── Generic fields ──────────────────────────────────────────── */
    .field { display: flex; flex-direction: column; gap: 0.375rem; }
    .field__label { font-size: 0.85rem; font-weight: 600; color: var(--color-fg, #1a1f2c); }
    .field__locked {
      padding: 0.5rem 0.625rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 8px;
      background: var(--color-bg-muted, #f4f6fa);
      color: var(--color-fg-muted, #5d6577);
      font-family: ui-monospace, SFMono-Regular, monospace;
      font-size: 0.85rem;
    }
    .field__select {
      width: 100%; font-family: inherit; font-size: 0.9rem;
      padding: 0.5rem 0.625rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 8px; background: var(--color-surface, #fff);
    }
    .field__hint { margin: 0; color: var(--color-fg-muted, #5d6577); font-size: 0.78rem; }
    .field__counter { margin: 0; color: var(--color-fg-muted, #5d6577); font-size: 0.75rem; text-align: right; }

    textarea {
      width: 100%; font-family: inherit; font-size: 0.9rem;
      padding: 0.625rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 8px; resize: vertical;
    }
    textarea:focus-visible {
      outline: 3px solid var(--color-primary, #1a3a8f);
      outline-offset: 1px;
    }

    /* ── Summary card ────────────────────────────────────────────── */
    .summary h2 { margin: 0 0 1rem; font-size: 1.1rem; }
    .summary__totals { margin: 0 0 1.25rem; display: grid; gap: 0.5rem; }
    .summary__totals div { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; }
    .summary__totals dt { display: flex; align-items: center; gap: 0.35rem; color: var(--color-fg-muted, #5d6577); margin: 0; font-size: 0.9rem; }
    .summary__totals dd { margin: 0; font-weight: 600; }
    .summary__total {
      padding-top: 0.75rem;
      border-top: 1px solid var(--color-border, #d8dde7);
    }
    .summary__total dt { color: var(--color-fg, #1a1f2c); font-weight: 700; font-size: 1rem; }
    .summary__total dd { font-size: 1.1rem; font-weight: 700; }
    .summary__tax-input {
      width: 4rem; font-family: inherit; font-size: 0.85rem;
      padding: 0.2rem 0.4rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 6px;
    }
    .summary__actions { display: grid; gap: 0.5rem; margin-top: 1rem; }

    .hint { color: var(--color-fg-muted, #5d6577); }
  `,
})
export class ContractorInvoicesComponent {
  private svc = inject(ContractorInvoicesService);

  protected readonly invoices = signal<InvoiceResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  protected readonly saving = signal(false);
  protected readonly sending = signal(false);
  protected readonly downloadingPdf = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly saveSuccess = signal<string | null>(null);

  /** id of the invoice loaded into the form; null = creating a new one. */
  protected readonly selectedInvoiceId = signal<string | null>(null);
  protected readonly selectedInvoice = computed(() =>
    this.invoices().find((x) => x.id === this.selectedInvoiceId()) ?? null,
  );
  protected readonly isEditing = computed(() => this.selectedInvoiceId() !== null);
  protected readonly isReadOnly = computed(() => {
    const inv = this.selectedInvoice();
    return inv !== null && inv.status !== 'Draft';
  });

  /** Status filter on the left rail. 'All' means no filter. */
  protected statusFilter: 'All' | InvoiceStatus = 'All';

  // ── Form state ────────────────────────────────────────────────────
  protected formClientName = '';
  protected formIssueDate = '';
  protected formDueDate = '';
  protected formPeriodStart = '';
  protected formPeriodEnd = '';
  protected formCurrency = 'USD';
  protected formTaxRate = '0';
  protected formNotes = '';
  protected readonly formLineItems = signal<DraftLineItem[]>([]);
  /** Server-minted number — only present when editing an existing invoice. */
  protected readonly formInvoiceNumber = signal('');

  // ── Currency catalog ──────────────────────────────────────────────
  protected readonly supportedCurrencies = [
    { code: 'USD', label: 'US Dollar' },
    { code: 'EUR', label: 'Euro' },
    { code: 'GBP', label: 'British Pound' },
    { code: 'CAD', label: 'Canadian Dollar' },
    { code: 'AUD', label: 'Australian Dollar' },
    { code: 'INR', label: 'Indian Rupee' },
  ];

  // ── Pagination ────────────────────────────────────────────────────
  protected readonly page = signal(1);
  protected readonly pageSize = 5;

  protected readonly filteredInvoices = computed(() => {
    const filter = this.statusFilter;
    const list = this.invoices();
    return filter === 'All' ? list : list.filter((x) => x.status === filter);
  });

  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredInvoices().length / this.pageSize)),
  );

  protected readonly pagedInvoices = computed(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.filteredInvoices().slice(start, start + this.pageSize);
  });

  protected readonly pageNumbers = computed(() => {
    const total = this.totalPages();
    return Array.from({ length: total }, (_, i) => i + 1);
  });

  // ── Totals as plain methods. computed() would cache on signal deps,
  //    but formTaxRate is a plain field bound via ngModel — re-evaluating
  //    every CD cycle is cheap (a handful of multiplies) and avoids the
  //    "stale tax" trap.
  protected formSubtotal(): number {
    return this.formLineItems().reduce((sum, li) => sum + this.lineItemAmount(li), 0);
  }

  protected formTaxAmount(): number {
    const rate = Number(this.formTaxRate) || 0;
    return Math.round((this.formSubtotal() * rate) / 100 * 100) / 100;
  }

  protected formTotal(): number {
    return Math.round((this.formSubtotal() + this.formTaxAmount()) * 100) / 100;
  }

  constructor() {
    this.startNewInvoice();
    this.refresh();

    // Clear the "Draft saved." banner a few seconds after it appears.
    effect(() => {
      if (this.saveSuccess()) {
        setTimeout(() => this.saveSuccess.set(null), 3500);
      }
    });
  }

  // ── Data ─────────────────────────────────────────────────────────
  protected refresh(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.svc.list().subscribe({
      next: (r) => {
        this.invoices.set(r.items);
        this.loading.set(false);
        // Re-resolve selection in case a list refresh dropped the row.
        const id = this.selectedInvoiceId();
        if (id && !r.items.some((x) => x.id === id)) {
          this.startNewInvoice();
        }
      },
      error: (e: unknown) => {
        this.loadError.set(toMessage(e));
        this.loading.set(false);
      },
    });
  }

  // ── Filter / pagination ──────────────────────────────────────────
  protected onFilterChange(): void {
    this.page.set(1);
  }

  protected goToPage(n: number): void {
    if (n < 1 || n > this.totalPages()) return;
    this.page.set(n);
  }

  // ── Selection ────────────────────────────────────────────────────
  protected onSelectInvoice(inv: InvoiceResponse): void {
    this.selectedInvoiceId.set(inv.id);
    this.formInvoiceNumber.set(inv.invoiceNumber);
    this.formClientName = inv.clientName;
    this.formIssueDate = inv.issueDateUtc;
    this.formDueDate = inv.dueDateUtc;
    this.formPeriodStart = inv.periodStartUtc;
    this.formPeriodEnd = inv.periodEndUtc;
    this.formCurrency = inv.currency;
    this.formTaxRate = String(inv.taxRate);
    this.formNotes = inv.notes ?? '';
    this.formLineItems.set(
      inv.lineItems.length > 0
        ? inv.lineItems.map((li) => ({
            description: li.description,
            hours: String(li.hours),
            rate: String(li.rate),
          }))
        : [emptyLineItem()],
    );
    this.formError.set(null);
  }

  protected startNewInvoice(): void {
    const today = isoToday();
    const due = isoOffsetDays(15);
    this.selectedInvoiceId.set(null);
    this.formInvoiceNumber.set('');
    this.formClientName = '';
    this.formIssueDate = today;
    this.formDueDate = due;
    this.formPeriodStart = today;
    this.formPeriodEnd = today;
    this.formCurrency = 'USD';
    this.formTaxRate = '10';
    this.formNotes = '';
    this.formLineItems.set([emptyLineItem()]);
    this.formError.set(null);
  }

  // ── Line items ───────────────────────────────────────────────────
  protected addLineItem(): void {
    this.formLineItems.update((list) => [...list, emptyLineItem()]);
  }

  protected removeLineItem(i: number): void {
    this.formLineItems.update((list) => {
      if (list.length === 1) return list;
      return list.filter((_, idx) => idx !== i);
    });
  }

  protected updateLineItem<K extends keyof DraftLineItem>(
    i: number,
    key: K,
    value: DraftLineItem[K],
  ): void {
    this.formLineItems.update((list) =>
      list.map((li, idx) => (idx === i ? { ...li, [key]: value } : li)),
    );
  }

  protected lineItemAmount(item: DraftLineItem): number {
    const h = Number(item.hours);
    const r = Number(item.rate);
    if (!Number.isFinite(h) || !Number.isFinite(r) || h < 0 || r < 0) return 0;
    return Math.round(h * r * 100) / 100;
  }

  // ── Submit / save ────────────────────────────────────────────────
  protected onSave(thenSubmit: boolean): void {
    if (this.isReadOnly()) return;
    this.formError.set(null);
    this.saveSuccess.set(null);

    const payload = this.buildPayload();
    if (typeof payload === 'string') {
      this.formError.set(payload);
      return;
    }

    const isUpdating = this.selectedInvoiceId() !== null;
    const action$ = isUpdating
      ? this.svc.update(this.selectedInvoiceId()!, payload)
      : this.svc.create(payload);

    (thenSubmit ? this.sending : this.saving).set(true);
    action$.subscribe({
      next: (saved) => {
        this.applySavedInvoice(saved);
        if (thenSubmit) {
          this.svc.submit(saved.id).subscribe({
            next: (submitted) => {
              this.applySavedInvoice(submitted);
              this.sending.set(false);
              this.saveSuccess.set(`Invoice ${submitted.invoiceNumber} submitted for review.`);
            },
            error: (e: unknown) => {
              this.sending.set(false);
              this.formError.set(toMessage(e));
            },
          });
        } else {
          this.saving.set(false);
          this.saveSuccess.set(`Draft ${saved.invoiceNumber} saved.`);
        }
      },
      error: (e: unknown) => {
        this.saving.set(false);
        this.sending.set(false);
        this.formError.set(toMessage(e));
      },
    });
  }

  protected submitSelected(): void {
    const id = this.selectedInvoiceId();
    if (!id) return;
    this.sending.set(true);
    this.formError.set(null);

    this.svc.submit(id).subscribe({
      next: (submitted) => {
        this.applySavedInvoice(submitted);
        this.sending.set(false);
        this.saveSuccess.set(`Invoice ${submitted.invoiceNumber} submitted for review.`);
      },
      error: (e: unknown) => {
        this.sending.set(false);
        this.formError.set(toMessage(e));
      },
    });
  }

  /**
   * Fetch the server-rendered PDF as a Blob and trigger a browser download.
   * Uses createObjectURL + a synthetic anchor click so no extra library is
   * needed and the file picks up the Content-Disposition filename from the
   * server response (Invoice-INV-2026-NNNN.pdf).
   */
  protected downloadPdf(): void {
    const invoice = this.selectedInvoice();
    if (!invoice) return;
    this.downloadingPdf.set(true);
    this.formError.set(null);

    this.svc.downloadPdf(invoice.id).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `Invoice-${invoice.invoiceNumber}.pdf`;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        window.URL.revokeObjectURL(url);
        this.downloadingPdf.set(false);
      },
      error: (e: unknown) => {
        this.downloadingPdf.set(false);
        this.formError.set(toMessage(e));
      },
    });
  }

  protected statusDetailFor(inv: InvoiceResponse): string {
    switch (inv.status) {
      case 'Paid': return inv.paidAtUtc ? `Paid ${inv.paidAtUtc.slice(0, 10)}` : 'Paid';
      case 'Submitted': return inv.submittedAtUtc ? `Submitted ${inv.submittedAtUtc.slice(0, 10)}` : 'Submitted';
      case 'Approved': return inv.reviewedAtUtc ? `Approved ${inv.reviewedAtUtc.slice(0, 10)}` : 'Approved';
      case 'Rejected': return inv.reviewedAtUtc ? `Rejected ${inv.reviewedAtUtc.slice(0, 10)}` : 'Rejected';
      default: return `Updated ${inv.updatedAtUtc.slice(0, 10)}`;
    }
  }

  /** Replace the row in the list (or prepend it) and re-select it. */
  private applySavedInvoice(saved: InvoiceResponse): void {
    this.invoices.update((list) => {
      const idx = list.findIndex((x) => x.id === saved.id);
      if (idx === -1) return [saved, ...list];
      const copy = [...list];
      copy[idx] = saved;
      return copy;
    });
    this.selectedInvoiceId.set(saved.id);
    this.formInvoiceNumber.set(saved.invoiceNumber);
  }

  /**
   * Returns a payload object on success, or an error message string if the
   * form is invalid. Trims line items the same way the server does — items
   * with no description and zero hours/rate are filtered out.
   */
  private buildPayload(): CreateInvoiceRequest | string {
    const issue = this.formIssueDate;
    const due = this.formDueDate;
    const start = this.formPeriodStart;
    const end = this.formPeriodEnd;

    if (!issue || !due || !start || !end) {
      return 'Issue date, due date, and work period are all required.';
    }
    if (due < issue) return 'Due date must be on or after the issue date.';
    if (end < start) return 'Period end must be on or after period start.';

    const taxRate = Number(this.formTaxRate);
    if (!Number.isFinite(taxRate) || taxRate < 0 || taxRate > 100) {
      return 'Tax rate must be a percentage between 0 and 100.';
    }

    const items: InvoiceLineItemRequest[] = this.formLineItems()
      .filter((li) =>
        li.description.trim().length > 0 ||
        Number(li.hours) > 0 ||
        Number(li.rate) > 0,
      )
      .map((li) => ({
        description: li.description.trim() || '—',
        hours: Number(li.hours) || 0,
        rate: Number(li.rate) || 0,
      }));

    if (items.length === 0) {
      return 'Add at least one line item before saving.';
    }
    for (const li of items) {
      if (li.hours < 0 || li.rate < 0) {
        return 'Line item hours and rate must be non-negative.';
      }
    }

    return {
      clientName: this.formClientName.trim() || null,
      issueDateUtc: issue,
      dueDateUtc: due,
      periodStartUtc: start,
      periodEndUtc: end,
      currency: this.formCurrency.trim().toUpperCase(),
      taxRate,
      lineItems: items,
      notes: this.formNotes.trim() || null,
    };
  }
}

// ── Local helpers ────────────────────────────────────────────────────

/** Working shape for a line item in the form (strings so empty inputs survive). */
interface DraftLineItem {
  description: string;
  hours: string;
  rate: string;
}

function emptyLineItem(): DraftLineItem {
  return { description: '', hours: '0', rate: '0' };
}

function isoToday(): string {
  return new Date().toISOString().slice(0, 10);
}

function isoOffsetDays(days: number): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

function toMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { detail?: string; title?: string } | null;
    return body?.detail ?? body?.title ?? error.message;
  }
  return error instanceof Error ? error.message : 'Unexpected error.';
}
