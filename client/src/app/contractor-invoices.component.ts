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
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { TenantBrandingResponse, TenantBrandingService } from './core/tenant/branding.service';
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
  QaLogoComponent,
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
    RouterLink,
    QaAlertComponent,
    QaButtonComponent,
    QaEmptyStateComponent,
    QaInputComponent,
    QaLogoComponent,
  ],
  template: `
    <main class="page">
      <header class="page__head">
        <div class="page__head-left">
          <h1>Invoices</h1>
          <p class="lede">Create, manage and track your invoices</p>
        </div>
        <div class="page__head-actions">
          <qa-button
            variant="ghost"
            (click)="toggleInvoicesPanel()"
          >{{ showInvoicesPanel() ? 'Hide invoices' : 'Your invoices' }}
            ({{ invoices().length }})
          </qa-button>
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
        <!-- ─────────────── Invoices panel (toggle) ─────────────── -->
        @if (showInvoicesPanel()) {
        <section class="rail rail--panel" aria-labelledby="rail-heading">
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
              description="Fill the form on the right and save a draft. Once saved, you can preview the PDF, download it, or export to CSV."
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
        }

        <!-- ─────────────── Form (full width) ─────────────── -->
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

          @if (branding(); as b) {
            <section class="letterhead-preview" aria-label="Your invoice letterhead">
              <div
                class="letterhead-preview__banner"
                [style.background]="bannerBackground(b.primaryColorHex)"
              >
                <div class="letterhead-preview__identity">
                  <qa-logo
                    size="46"
                    alt=""
                    class="letterhead-preview__mark"
                  ></qa-logo>
                  <div class="letterhead-preview__identity-text">
                    <strong>{{ b.legalName || b.displayName || 'Your Company' }}</strong>
                    <span class="letterhead-preview__sub">Invoice</span>
                  </div>
                </div>
                <div
                  class="letterhead-preview__strip"
                  [style.background]="b.accentColorHex || '#e6c9a8'"
                ></div>
              </div>
              <div class="letterhead-preview__meta">
                <span>Matches the banner at the top of every invoice PDF.</span>
                <a routerLink="/settings/branding" class="letterhead-preview__link">Edit in Branding settings →</a>
              </div>
            </section>
          }

          <fieldset class="section" [disabled]="isReadOnly()">
            <legend>Invoice details</legend>
            <div class="grid grid--3">
              <div class="field">
                <label class="field__label" for="invoice-number-input">Invoice number</label>
                <input
                  id="invoice-number-input"
                  class="field__input"
                  type="text"
                  [(ngModel)]="formInvoiceNumberInput"
                  name="invoiceNumber"
                  placeholder="Leave blank to auto-generate"
                  [disabled]="isReadOnly() || isEditing()"
                />
                <p class="field__hint">
                  @if (isEditing() && formInvoiceNumber()) {
                    Issued as <strong>{{ formInvoiceNumber() }}</strong>. Locked once saved.
                  } @else if (formInvoiceNumberInput) {
                    Custom number — must be unique for your tenant.
                  } @else {
                    Server auto-mints INV-{{ formYear() }}-NNNN on save.
                  }
                </p>
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

          @if (branding(); as b) {
            <fieldset class="section section--remit" [disabled]="isReadOnly()">
              <legend>Remit-to (your bank)</legend>
              <p class="section__hint section__hint--remit">
                Defaults come from your <a routerLink="/settings/branding">branding settings</a>.
                Leave a field blank to inherit; type to override just for this invoice.
              </p>
              <div class="grid grid--4">
                <label class="field">
                  <span class="field__label">Bank name</span>
                  <input
                    class="field__input"
                    type="text"
                    [(ngModel)]="formRemitBankName"
                    name="remitBankName"
                    [placeholder]="b.bankName || '—'"
                  />
                </label>
                <label class="field">
                  <span class="field__label">Account number</span>
                  <input
                    class="field__input field__input--mono"
                    type="text"
                    [(ngModel)]="formRemitAccountNumber"
                    name="remitAccountNumber"
                    [placeholder]="b.bankAccountNumber || '—'"
                  />
                </label>
                <label class="field">
                  <span class="field__label">Routing number</span>
                  <input
                    class="field__input field__input--mono"
                    type="text"
                    [(ngModel)]="formRemitRoutingNumber"
                    name="remitRoutingNumber"
                    [placeholder]="b.bankRoutingNumber || '—'"
                  />
                </label>
                <label class="field">
                  <span class="field__label">Phone</span>
                  <input
                    class="field__input"
                    type="tel"
                    [(ngModel)]="formRemitContactPhone"
                    name="remitContactPhone"
                    [placeholder]="b.contactPhone || '—'"
                  />
                </label>
              </div>
            </fieldset>
          }

          <fieldset class="section" [disabled]="isReadOnly()">
            <legend>Client</legend>
            <div class="grid grid--2">
              <qa-input
                label="Client name"
                [(ngModel)]="formClientName"
                name="clientName"
                hint="The end company you are billing (e.g., NYS-State of New York)."
              ></qa-input>
              <qa-input
                label="Vendor"
                [(ngModel)]="formVendorName"
                name="vendorName"
                hint="Sub-department or vendor reference, if any (e.g., DOCCS)."
              ></qa-input>
            </div>
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
            <p class="section__hint">One row per work-week. Pick the Monday and we'll compute the Sunday end. Hours = Days × Hours/day. Amount = Hours × Rate.</p>

            <div class="weeks">
              @for (item of formLineItems(); track $index; let i = $index) {
                <article class="week">
                  <header class="week__head">
                    <span class="week__num">Week {{ i + 1 }}</span>
                    @if (item.weekStart) {
                      <span class="week__range">
                        {{ item.weekStart }} → {{ lineItemWeekEnd(item) || '—' }}
                      </span>
                    }
                    <button
                      type="button"
                      class="week__delete"
                      (click)="removeLineItem(i)"
                      [disabled]="formLineItems().length === 1"
                      aria-label="Delete this week"
                    >Remove</button>
                  </header>

                  <div class="week__grid">
                    <label class="field field--span2">
                      <span class="field__label">Description</span>
                      <input
                        class="field__input"
                        type="text"
                        [ngModel]="item.description"
                        (ngModelChange)="updateLineItem(i, 'description', $event)"
                        name="li-desc-{{ i }}"
                        placeholder="e.g., NYS-DOCCS"
                      />
                    </label>

                    <label class="field">
                      <span class="field__label">Week of (Monday)</span>
                      <input
                        class="field__input"
                        type="date"
                        [ngModel]="item.weekStart"
                        (ngModelChange)="updateLineItem(i, 'weekStart', $event)"
                        name="li-weekstart-{{ i }}"
                      />
                    </label>

                    <label class="field">
                      <span class="field__label">Days</span>
                      <input
                        class="field__input field__input--num"
                        type="number" min="0" max="7" step="0.5"
                        [ngModel]="item.daysWorked"
                        (ngModelChange)="updateLineItem(i, 'daysWorked', $event)"
                        name="li-days-{{ i }}"
                      />
                    </label>

                    <label class="field">
                      <span class="field__label">Hours / day</span>
                      <input
                        class="field__input field__input--num"
                        type="number" min="0" step="0.25"
                        [ngModel]="item.hoursPerDay"
                        (ngModelChange)="updateLineItem(i, 'hoursPerDay', $event)"
                        name="li-hpd-{{ i }}"
                      />
                    </label>

                    <label class="field">
                      <span class="field__label">Rate</span>
                      <input
                        class="field__input field__input--num"
                        type="number" min="0" step="0.01"
                        [ngModel]="item.rate"
                        (ngModelChange)="updateLineItem(i, 'rate', $event)"
                        name="li-rate-{{ i }}"
                      />
                    </label>
                  </div>

                  <div class="week__totals">
                    <span class="week__formula">
                      {{ item.daysWorked || 0 }} × {{ item.hoursPerDay || 0 }} =
                      <strong>{{ lineItemHours(item) | number:'1.0-2' }}</strong> hrs
                    </span>
                    <span class="week__amount">
                      {{ formCurrency }} <strong>{{ lineItemAmount(item) | number:'1.2-2' }}</strong>
                    </span>
                  </div>

                  <label class="field week__notes">
                    <span class="field__label">Notes (optional)</span>
                    <textarea
                      rows="2"
                      [ngModel]="item.notes"
                      (ngModelChange)="updateLineItem(i, 'notes', $event)"
                      name="li-notes-{{ i }}"
                      placeholder="Additional info for this week (overtime, PTO, etc.)"
                    ></textarea>
                  </label>
                </article>
              }
              <button type="button" class="weeks__add" (click)="addLineItem()">+ Add another week</button>
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
              <dd>{{ formCurrency }} {{ formTotal() | number:'1.2-2' }}</dd>
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
                (click)="onSave(true)"
              >{{ sending() ? 'Sending…' : 'Save and send' }}</qa-button>
              <qa-button
                variant="ghost"
                [disabled]="saving() || sending()"
                (click)="onSave(false)"
              >{{ saving() ? 'Saving…' : 'Save draft' }}</qa-button>
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
                [disabled]="downloadingPdf() || loadingPreview()"
                (click)="openPreview()"
              >{{ loadingPreview() ? 'Loading preview…' : 'Preview invoice' }}</qa-button>
              <qa-button
                variant="ghost"
                [disabled]="downloadingPdf()"
                (click)="downloadPdf()"
              >{{ downloadingPdf() ? 'Generating PDF…' : 'Download PDF' }}</qa-button>
              <qa-button
                variant="ghost"
                [disabled]="downloadingCsv()"
                (click)="downloadCsv()"
              >{{ downloadingCsv() ? 'Exporting CSV…' : 'Export to CSV' }}</qa-button>
            </div>
          } @else if (!isReadOnly()) {
            <p class="summary__after-save">
              <span class="summary__after-save-icon" aria-hidden="true">✦</span>
              After saving: Preview, Download PDF, and Export CSV unlock here.
            </p>
          }
        </aside>
      </form>

      @if (previewUrl()) {
        <div class="preview-modal" role="dialog" aria-modal="true" aria-label="Invoice preview">
          <button
            type="button"
            class="preview-modal__shroud"
            (click)="closePreview()"
            aria-label="Close preview"
          ></button>
          <div class="preview-modal__panel">
            <header class="preview-modal__head">
              <h2>Invoice preview</h2>
              <div class="preview-modal__head-actions">
                <a
                  class="preview-modal__open-tab"
                  [href]="previewUrl()"
                  target="_blank"
                  rel="noopener"
                >Open in new tab ↗</a>
                <button type="button" class="preview-modal__close" (click)="closePreview()" aria-label="Close preview">×</button>
              </div>
            </header>
            <iframe
              class="preview-modal__frame"
              [src]="previewSafeUrl()"
              title="Invoice PDF"
            ></iframe>
            <p class="preview-modal__hint">
              If the preview doesn't render below, use "Open in new tab" above —
              some browsers block inline PDF embeds.
            </p>
            <footer class="preview-modal__foot">
              <qa-button variant="primary" (click)="downloadPdf()">Download PDF</qa-button>
              <qa-button variant="ghost" (click)="closePreview()">Close</qa-button>
            </footer>
          </div>
        </div>
      }
    </main>
  `,
  styles: `
    :host {
      display: block;
      background:
        radial-gradient(1200px 600px at 50% -10%, rgba(26, 58, 143, 0.06), transparent),
        #f7f9fc;
      min-height: 100vh;
    }
    .page { width: min(1480px, 100%); margin: 0 auto; padding: 1.75rem 1.5rem 4rem; }

    /* ── Header ──────────────────────────────────────────────────── */
    .page__head {
      display: flex; align-items: flex-start; justify-content: space-between;
      gap: 1rem; flex-wrap: wrap; margin-bottom: 1.75rem;
    }
    .page__head-left h1 { margin: 0 0 0.375rem; font-size: 1.875rem; letter-spacing: -0.01em; }
    .lede { color: var(--color-fg-muted, #5d6577); margin: 0; font-size: 0.95rem; }
    .page__head-actions { display: flex; gap: 0.625rem; flex-wrap: wrap; align-items: center; }

    /* ── Layout ───────────────────────────────────────────────────
       Default: stacked single column (mobile + narrow desktops).
       ≥1100px: form ~74% / summary ~26% side-by-side with summary
       sticky so the Save / Send action buttons stay reachable as
       the contractor scrolls the long form. */
    .layout {
      display: grid;
      grid-template-columns: 1fr;
      gap: 1.25rem;
      align-items: start;
    }
    .rail--panel {
      /* Toggleable invoices panel renders above the form. Cap its height
         so the form stays in view; vertically scroll inside if needed. */
      max-height: 480px;
      overflow-y: auto;
    }
    @media (min-width: 1100px) {
      .layout {
        grid-template-columns: minmax(0, 1fr) 360px;
        column-gap: 1.5rem;
      }
      .layout > section.rail--panel,
      .layout > section.form { grid-column: 1; }
      .layout > aside.summary {
        grid-column: 2;
        grid-row: 1 / span 99;  /* hold the right column across rows */
        position: sticky;
        top: 1rem;
        align-self: start;
        max-height: calc(100vh - 2rem);
        overflow-y: auto;
      }
    }

    /* ── Card shell ──────────────────────────────────────────────── */
    section.rail, section.form, aside.summary {
      background: #ffffff;
      border: 1px solid var(--color-border, #e2e6ee);
      border-radius: 16px;
      padding: 1.5rem;
      box-shadow: 0 2px 6px rgba(15, 23, 42, 0.05),
                  0 1px 2px rgba(15, 23, 42, 0.04);
    }
    /* Headlines in primary blue so each card has a clear identity */
    section.rail h2, section.form h2, aside.summary h2 {
      color: var(--color-primary, #1a3a8f);
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
      padding: 0.875rem 1rem;
      border: 1px solid var(--color-border, #e2e6ee);
      border-radius: 12px;
      background: #ffffff;
      text-align: left;
      cursor: pointer;
      transition: border-color 0.12s, background 0.12s, box-shadow 0.12s;
    }
    .row:hover {
      border-color: var(--color-primary, #1a3a8f);
      box-shadow: 0 2px 8px rgba(26, 58, 143, 0.08);
    }
    .row--selected {
      border-color: var(--color-primary, #1a3a8f);
      background: var(--color-primary-soft, #e7ecf6);
      box-shadow: 0 2px 8px rgba(26, 58, 143, 0.12);
    }
    .row__number { color: var(--color-primary, #1a3a8f); }
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
    .form__head {
      display: flex; align-items: flex-start; justify-content: space-between;
      gap: 1rem; margin-bottom: 1.5rem; flex-wrap: wrap;
      padding-bottom: 1rem; border-bottom: 1px solid var(--color-border, #d8dde7);
    }
    .form h2 { margin: 0 0 0.375rem; font-size: 1.25rem; letter-spacing: -0.005em; }
    .form__head p { margin: 0; color: var(--color-fg-muted, #5d6577); font-size: 0.9rem; }
    .section { border: 0; padding: 0; margin: 0 0 1.75rem; }
    .section:last-of-type { margin-bottom: 0; }
    .section legend {
      font-size: 0.72rem; font-weight: 800; margin-bottom: 0.875rem; padding: 0;
      color: var(--color-primary, #1a3a8f);
      text-transform: uppercase; letter-spacing: 0.1em;
    }
    .grid { display: grid; gap: 1rem; }
    .grid--2 { grid-template-columns: 1fr 1fr; }
    .grid--4 { grid-template-columns: repeat(4, minmax(0, 1fr)); }
    @media (max-width: 880px) {
      .grid--4 { grid-template-columns: 1fr 1fr; }
    }
    @media (max-width: 540px) {
      .grid--4 { grid-template-columns: 1fr; }
    }

    /* ── Remit-to override section (qa005) ──────────────────────
       Soft blue card that visually separates the per-invoice
       overrides from the regular billing details. Inputs use the
       branding default as placeholder so the user can see what
       leaving the field blank will produce. */
    .section--remit {
      background: var(--color-primary-soft, #e7ecf6);
      border: 1px dashed rgba(26, 58, 143, 0.25);
      border-radius: 12px;
      padding: 1rem 1.125rem 1.125rem;
      margin-bottom: 1.5rem;
    }
    .section--remit legend {
      padding: 0 0.5rem; background: transparent;
      color: var(--color-primary, #1a3a8f);
    }
    .section--remit .field__input {
      background: #ffffff;
    }
    .section__hint--remit {
      margin: 0 0 0.875rem;
      font-size: 0.78rem; color: var(--color-fg-muted, #5d6577);
    }
    .section__hint--remit a {
      color: var(--color-primary, #1a3a8f); text-decoration: underline;
    }
    /* Invoice details: number takes a full row at narrow widths so its
       hint text doesn't wrap into a useless single-word column. Issue +
       Due dates share the second row. At wider widths everything is on
       one row. */
    .grid--3 {
      grid-template-columns: minmax(180px, 1.4fr) minmax(140px, 1fr) minmax(140px, 1fr);
    }
    @media (max-width: 880px) {
      .grid--3 { grid-template-columns: 1fr 1fr; }
      .grid--3 > :first-child { grid-column: 1 / -1; }
    }
    @media (max-width: 540px) {
      .grid--3, .grid--2 { grid-template-columns: 1fr; }
      .grid--3 > :first-child { grid-column: auto; }
    }

    /* ── Items table ─────────────────────────────────────────────── */
    .items {
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 10px;
      overflow: hidden;
      background: var(--color-surface, #fff);
    }
    .items__head, .items__row {
      display: grid;
      /* Description gets the leftovers (minmax 0 1fr so it never blows out
         the row). Hours/Rate/Amount/Actions are fixed widths. */
      grid-template-columns: minmax(0, 1fr) 96px 112px 132px 40px;
      column-gap: 12px;
      padding: 12px 16px;
      align-items: center;
    }
    .items__head {
      background: #f8fafc;
      font-size: 11px; font-weight: 700;
      color: var(--color-fg-muted, #5d6577);
      text-transform: uppercase; letter-spacing: 0.06em;
    }
    .items__head .items__col--hours,
    .items__head .items__col--rate,
    .items__head .items__col--amount { text-align: right; }

    .items__row { border-top: 1px solid var(--color-border, #d8dde7); }
    .items__input {
      width: 100%; min-width: 0;
      font-family: inherit; font-size: 0.9rem;
      padding: 0.5rem 0.625rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 7px;
      background: var(--color-surface, #fff);
      text-align: left;
    }
    .items__input.items__col--hours,
    .items__input.items__col--rate { text-align: right; }
    .items__input:focus-visible {
      outline: 3px solid var(--color-primary, #1a3a8f);
      outline-offset: 1px;
    }
    .items__amount { font-weight: 600; text-align: right; white-space: nowrap; }
    .items__delete {
      background: #fff; border: 1px solid var(--color-border, #d8dde7); color: #991b1b;
      width: 32px; height: 32px; border-radius: 8px;
      cursor: pointer; font-size: 1.1rem; line-height: 1;
      display: inline-flex; align-items: center; justify-content: center;
    }
    .items__delete:hover { background: #fee2e2; border-color: #fca5a5; }
    .items__delete:disabled { opacity: 0.3; cursor: not-allowed; }
    .items__add {
      display: block; width: 100%;
      padding: 0.875rem; text-align: center;
      background: #f8fafc; border: 0; border-top: 1px dashed var(--color-border, #d8dde7);
      color: var(--color-primary, #1a3a8f); font-weight: 600; font-family: inherit; font-size: 0.875rem;
      cursor: pointer;
    }
    .items__add:hover { background: var(--color-primary-soft, #e7ecf6); }
    @media (max-width: 720px) {
      .items__head { display: none; }
      .items__row { grid-template-columns: 1fr; row-gap: 0.5rem; }
      .items__row .items__col--hours::before { content: 'Hours: '; color: var(--color-fg-muted, #5d6577); font-weight: 600; margin-right: 0.5rem; }
      .items__row .items__col--rate::before { content: 'Rate: '; color: var(--color-fg-muted, #5d6577); font-weight: 600; margin-right: 0.5rem; }
      .items__row .items__col--amount { text-align: left; }
      .items__delete { justify-self: end; }
    }

    /* ── Generic fields ──────────────────────────────────────────── */
    .field { display: flex; flex-direction: column; gap: 0.375rem; }
    .field__label { font-size: 0.85rem; font-weight: 600; color: var(--color-fg, #1a1f2c); }
    .field__number {
      padding: 0.625rem 0.75rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 8px;
      background: var(--color-primary-soft, #e7ecf6);
      color: var(--color-primary, #1a3a8f);
      font-family: ui-monospace, SFMono-Regular, monospace;
      font-size: 0.95rem; font-weight: 700; letter-spacing: 0.02em;
      white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
    }
    .field__badge {
      display: inline-flex; align-items: center; gap: 0.5rem;
      align-self: flex-start;
      padding: 0.45rem 0.75rem;
      border: 1px dashed var(--color-border, #d8dde7);
      border-radius: 999px;
      background: #f8fafc;
      color: var(--color-fg-muted, #5d6577);
      font-size: 0.8rem; font-weight: 500;
      white-space: nowrap;
    }
    .field__badge-dot {
      width: 6px; height: 6px; border-radius: 50%;
      background: var(--color-primary, #1a3a8f);
      flex-shrink: 0;
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
    /* Stickiness is applied only at wide widths inside the .layout
       media query above; on narrow screens summary follows the form
       normally so the user isn't fighting overlap. */
    .summary { position: static; }
    .summary h2 {
      margin: 0 0 1.25rem; font-size: 1.25rem; letter-spacing: -0.005em;
      padding-bottom: 0.875rem; border-bottom: 1px solid var(--color-border, #d8dde7);
    }
    .summary__totals { margin: 0 0 1.5rem; display: grid; gap: 0.625rem; }
    .summary__totals div { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; }
    .summary__totals dt {
      display: flex; align-items: center; gap: 0.5rem;
      color: var(--color-fg-muted, #5d6577); margin: 0; font-size: 0.875rem;
    }
    .summary__totals dd { margin: 0; font-weight: 600; font-size: 0.95rem; font-variant-numeric: tabular-nums; }
    .summary__total {
      padding-top: 1rem;
      border-top: 1px solid var(--color-border, #d8dde7);
    }
    .summary__total dt { color: var(--color-fg, #1a1f2c); font-weight: 700; font-size: 1rem; }
    .summary__total dd {
      font-size: 1.375rem; font-weight: 700;
      color: var(--color-primary, #1a3a8f);
      font-variant-numeric: tabular-nums;
    }
    .summary__tax-input {
      width: 4.5rem; font-family: inherit; font-size: 0.85rem;
      padding: 0.25rem 0.5rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 6px; text-align: right;
      font-variant-numeric: tabular-nums;
    }
    .summary__actions { display: grid; gap: 0.5rem; margin-top: 1.25rem; }
    .summary__actions--pdf { margin-top: 0.5rem; padding-top: 0.5rem; border-top: 1px dashed var(--color-border, #d8dde7); }
    .summary__after-save {
      margin: 1.25rem 0 0;
      padding: 0.75rem 0.875rem;
      border-radius: 10px;
      background: var(--color-primary-soft, #e7ecf6);
      color: var(--color-primary, #1a3a8f);
      font-size: 0.825rem; line-height: 1.4;
      display: flex; align-items: flex-start; gap: 0.5rem;
    }
    .summary__after-save-icon { font-size: 0.95rem; }

    .hint { color: var(--color-fg-muted, #5d6577); }
    .section__hint { margin: 0 0 1rem; color: var(--color-fg-muted, #5d6577); font-size: 0.85rem; }

    /* Letterhead preview at the top of the form so the user always sees
       what their invoice's banner will look like. Pulls from /settings/branding
       (backend falls back to Quantam defaults for unset fields). */
    .letterhead-preview {
      margin: 0 0 1.5rem;
      border-radius: 12px;
      border: 1px solid var(--color-border, #e2e6ee);
      overflow: hidden;
      background: #fff;
    }
    .letterhead-preview__banner {
      position: relative;
      padding: 1.25rem 1.5rem 1.75rem;
      color: #fff;
      box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.08);
    }
    .letterhead-preview__strip {
      position: absolute; inset: auto 0 0 0;
      height: 6px;
    }
    .letterhead-preview__identity {
      display: flex; align-items: center; gap: 1rem;
    }
    /* Make the logo's ring white and keep the tick accent so it pops
       against the navy banner — same treatment as the home hero. */
    .letterhead-preview__mark {
      --color-primary: #ffffff;
      --color-accent: #e6c9a8;
      flex-shrink: 0;
    }
    .letterhead-preview__identity-text {
      display: flex; flex-direction: column; gap: 0.125rem; min-width: 0;
    }
    .letterhead-preview__identity-text strong {
      font-size: 1.15rem; font-weight: 700; letter-spacing: -0.005em;
    }
    .letterhead-preview__sub {
      font-size: 0.825rem; opacity: 0.88;
    }
    .letterhead-preview__meta {
      display: flex; justify-content: space-between; align-items: center;
      gap: 0.75rem; flex-wrap: wrap;
      padding: 0.625rem 1rem;
      background: #f8fafc;
      font-size: 0.78rem;
      color: var(--color-fg-muted, #5d6577);
    }
    .letterhead-preview__link {
      color: var(--color-primary, #1a3a8f);
      font-weight: 600; text-decoration: none;
    }
    .letterhead-preview__link:hover { text-decoration: underline; }

    /* ── Week cards (replace old items table) ────────────────────── */
    .weeks { display: grid; gap: 1rem; }
    .week {
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 12px;
      padding: 1rem 1.125rem;
      background: var(--color-surface, #fff);
    }
    .week__head {
      display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap;
      margin-bottom: 0.875rem;
    }
    .week__num {
      font-weight: 700; font-size: 0.95rem;
      color: var(--color-primary, #1a3a8f);
    }
    .week__range { color: var(--color-fg-muted, #5d6577); font-size: 0.85rem; }
    .week__delete {
      margin-left: auto;
      background: transparent; border: 1px solid var(--color-border, #d8dde7);
      color: #991b1b; padding: 0.3rem 0.625rem; border-radius: 7px;
      font-family: inherit; font-size: 0.8rem; cursor: pointer;
    }
    .week__delete:hover { background: #fee2e2; border-color: #fca5a5; }
    .week__delete:disabled { opacity: 0.3; cursor: not-allowed; }

    .week__grid {
      display: grid;
      grid-template-columns: repeat(5, minmax(0, 1fr));
      gap: 0.75rem;
      margin-bottom: 0.875rem;
    }
    .week__grid .field--span2 { grid-column: span 2; }
    @media (max-width: 720px) {
      .week__grid { grid-template-columns: 1fr 1fr; }
      .week__grid .field--span2 { grid-column: 1 / -1; }
    }
    @media (max-width: 480px) {
      .week__grid { grid-template-columns: 1fr; }
      .week__grid .field--span2 { grid-column: auto; }
    }

    .field__input {
      width: 100%; min-width: 0;
      font-family: inherit; font-size: 0.9rem;
      padding: 0.5rem 0.625rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 7px;
      background: var(--color-surface, #fff);
    }
    .field__input--num { text-align: right; font-variant-numeric: tabular-nums; }
    .field__input--mono {
      font-family: ui-monospace, SFMono-Regular, monospace;
      letter-spacing: 0.02em;
    }
    .field__input:focus-visible {
      outline: 3px solid var(--color-primary, #1a3a8f);
      outline-offset: 1px;
    }

    .week__totals {
      display: flex; justify-content: space-between; align-items: center;
      gap: 1rem; padding: 0.625rem 0.875rem;
      background: #f8fafc; border-radius: 8px;
      font-size: 0.9rem; flex-wrap: wrap;
    }
    .week__formula { color: var(--color-fg-muted, #5d6577); }
    .week__formula strong { color: var(--color-fg, #1a1f2c); }
    .week__amount { font-size: 1rem; color: var(--color-fg-muted, #5d6577); }
    .week__amount strong { color: var(--color-primary, #1a3a8f); font-size: 1.05rem; font-variant-numeric: tabular-nums; }

    .week__notes { display: block; margin-top: 0.875rem; }
    .week__notes textarea {
      width: 100%; font-family: inherit; font-size: 0.9rem;
      padding: 0.5rem 0.625rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 7px; resize: vertical;
    }
    .week__notes textarea:focus-visible {
      outline: 3px solid var(--color-primary, #1a3a8f);
      outline-offset: 1px;
    }

    .weeks__add {
      width: 100%; padding: 0.875rem;
      background: var(--color-primary-soft, #e7ecf6);
      border: 1px dashed var(--color-primary, #1a3a8f);
      border-radius: 10px;
      color: var(--color-primary, #1a3a8f); font-weight: 600;
      font-family: inherit; font-size: 0.9rem; cursor: pointer;
    }
    .weeks__add:hover { background: #d8e0f0; }

    /* ── Preview modal ───────────────────────────────────────────── */
    .preview-modal {
      position: fixed; inset: 0; z-index: 100;
      display: flex; align-items: stretch; justify-content: center;
    }
    .preview-modal__shroud {
      position: absolute; inset: 0;
      background: rgba(15, 23, 42, 0.6);
      /* Reset default <button> styling so the shroud looks like a plain
         backdrop. Lives as a button (not a div) so keyboard users can
         dismiss the modal — see WCAG 2.1.1 keyboard accessibility. */
      border: 0; padding: 0; appearance: none;
      cursor: pointer;
    }
    .preview-modal__shroud:focus-visible {
      outline: 2px dashed rgba(255, 255, 255, 0.7);
      outline-offset: -8px;
    }
    .preview-modal__panel {
      position: relative; z-index: 1;
      margin: 2rem auto; max-width: 960px; width: calc(100% - 2rem);
      max-height: calc(100vh - 4rem);
      background: var(--color-surface, #fff);
      border-radius: 14px;
      display: flex; flex-direction: column;
      overflow: hidden;
      box-shadow: 0 20px 60px rgba(15, 23, 42, 0.3);
    }
    .preview-modal__head {
      display: flex; justify-content: space-between; align-items: center;
      padding: 0.875rem 1.25rem;
      border-bottom: 1px solid var(--color-border, #d8dde7);
    }
    .preview-modal__head h2 { margin: 0; font-size: 1.05rem; }
    .preview-modal__close {
      background: none; border: 0; font-size: 1.5rem; line-height: 1;
      cursor: pointer; padding: 0.25rem 0.625rem; border-radius: 7px;
      color: var(--color-fg-muted, #5d6577);
    }
    .preview-modal__close:hover { background: var(--color-bg-muted, #f4f6fa); }
    .preview-modal__head-actions { display: flex; align-items: center; gap: 0.625rem; }
    .preview-modal__open-tab {
      color: var(--color-primary, #1a3a8f);
      font-size: 0.85rem; font-weight: 600;
      text-decoration: none;
      padding: 0.35rem 0.625rem; border-radius: 7px;
      transition: background 0.12s;
    }
    .preview-modal__open-tab:hover { background: var(--color-primary-soft, #e7ecf6); }
    .preview-modal__hint {
      margin: 0; padding: 0.625rem 1.25rem;
      font-size: 0.78rem; color: var(--color-fg-muted, #5d6577);
      background: #f8fafc;
      border-top: 1px solid var(--color-border, #e2e6ee);
    }
    .preview-modal__frame {
      flex: 1 1 auto; width: 100%; border: 0;
      background: #f4f6fa;
      min-height: 400px;
    }
    .preview-modal__foot {
      display: flex; gap: 0.625rem; justify-content: flex-end;
      padding: 0.875rem 1.25rem;
      border-top: 1px solid var(--color-border, #d8dde7);
    }
  `,
})
export class ContractorInvoicesComponent {
  private svc = inject(ContractorInvoicesService);
  private brandingSvc = inject(TenantBrandingService);
  private sanitizer = inject(DomSanitizer);

  /**
   * Hard-coded fallback so the letterhead preview ALWAYS renders, even
   * when /api/v1/tenant/branding returns 500 / 401 or hasn't loaded yet.
   * Mirrors the server-side BrandingDefaults static so the form preview
   * and the rendered PDF stay in lockstep.
   */
  private static readonly QUANTAM_DEFAULTS: TenantBrandingResponse = {
    displayName: 'Mohammed Khan',
    legalName: 'Quantamanalytics LLC',
    contactEmail: 'mohammed.khan@quantamanalytics.com',
    contactPhone: '909-560-3095',
    addressLine1: null,
    addressLine2: null,
    city: null,
    stateRegion: null,
    postalCode: null,
    country: null,
    bankName: 'Chase',
    bankAccountNumber: '993681185',
    bankRoutingNumber: '021202337',
    defaultHourlyRate: 55,
    defaultCurrency: 'USD',
    defaultPaymentTermsDays: 14,
    primaryColorHex: '#1a2d5a',
    accentColorHex: '#e6c9a8',
    hasLogo: false,
    updatedAtUtc: new Date().toISOString(),
  };

  /**
   * Loaded once on init; starts with QUANTAM_DEFAULTS so the preview
   * is never empty even before the API call lands. If the call returns
   * a populated branding row, we swap to that.
   */
  protected readonly branding = signal<TenantBrandingResponse>(
    ContractorInvoicesComponent.QUANTAM_DEFAULTS,
  );

  protected readonly invoices = signal<InvoiceResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  protected readonly saving = signal(false);
  protected readonly sending = signal(false);
  protected readonly downloadingPdf = signal(false);
  protected readonly downloadingCsv = signal(false);
  protected readonly loadingPreview = signal(false);
  protected readonly previewUrl = signal<string | null>(null);
  /** Sanitized SafeUrl for the iframe. Recomputed when previewUrl changes. */
  protected previewSafeUrl(): SafeResourceUrl | null {
    const url = this.previewUrl();
    return url ? this.sanitizer.bypassSecurityTrustResourceUrl(url) : null;
  }
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

  /** Invoices panel is hidden by default — form gets the focus on load. */
  protected readonly showInvoicesPanel = signal(false);
  protected toggleInvoicesPanel(): void {
    this.showInvoicesPanel.update((v) => !v);
  }

  /**
   * Build a subtle two-stop gradient for the letterhead preview banner.
   * Matches the home-page hero banner so the invoice feels like the
   * same product — a flat fill looks cheap; a gradient gives it depth.
   */
  protected bannerBackground(primaryHex: string | null | undefined): string {
    const base = primaryHex || '#1a2d5a';
    return `radial-gradient(900px 360px at 20% 0%, rgba(255,255,255,0.08), transparent 70%), linear-gradient(135deg, ${base} 0%, ${base} 60%, #0d2155 100%)`;
  }

  // ── Form state ────────────────────────────────────────────────────
  protected formClientName = '';
  /** Sub-department / vendor reference, appended to the client line on the PDF. */
  protected formVendorName = '';
  protected formIssueDate = '';
  protected formDueDate = '';
  protected formPeriodStart = '';
  protected formPeriodEnd = '';
  protected formCurrency = 'USD';
  protected formTaxRate = '0';
  protected formNotes = '';
  /** User's custom override; if blank on save, server mints. */
  protected formInvoiceNumberInput = '';
  /**
   * qa005 — per-invoice Remit-to overrides. Empty string means "inherit
   * the tenant's branding default" — the inputs render with the
   * branding value as placeholder so the user sees what they'd get if
   * they leave it blank.
   */
  protected formRemitBankName = '';
  protected formRemitAccountNumber = '';
  protected formRemitRoutingNumber = '';
  protected formRemitContactPhone = '';
  protected readonly formLineItems = signal<DraftLineItem[]>([]);
  /** Server-minted number — only present when editing an existing invoice. */
  protected readonly formInvoiceNumber = signal('');

  /** Year hint in the hint text under the Invoice number input. */
  protected formYear(): number {
    if (this.formIssueDate) {
      const y = Number(this.formIssueDate.slice(0, 4));
      if (Number.isFinite(y)) return y;
    }
    return new Date().getFullYear();
  }

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

    // Pre-load the tenant's branding so the form can show a preview of
    // the letterhead that'll appear on the saved invoice / PDF. Backend
    // fills in Quantam defaults for any unset field.
    this.brandingSvc.get().subscribe({
      // Merge API response over defaults so any blank fields keep the
      // Quantam fallback (server already does this, but be defensive).
      next: (b) => this.branding.set({
        ...ContractorInvoicesComponent.QUANTAM_DEFAULTS,
        ...Object.fromEntries(Object.entries(b).filter(([, v]) => v !== null && v !== undefined)),
      }),
      // On error: keep the defaults — preview still renders.
      error: () => { /* no-op, defaults already set */ },
    });

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
        // Soft-fail: a 401/412 here means the user isn't fully bootstrapped
        // (no tenant, no subject claim yet). Don't dominate the page with
        // a red banner — show the empty state instead and let them create
        // their first invoice. The create call has its own error handling.
        // Soft-fail any backend error on initial list — show empty state
        // instead of a scary banner. The user can still create a new
        // invoice; create has its own error handling that surfaces the
        // real problem if it persists.
        if (e instanceof HttpErrorResponse) {
          this.invoices.set([]);
          this.loadError.set(null);
        } else {
          this.loadError.set(toMessage(e));
        }
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
    this.formInvoiceNumberInput = inv.invoiceNumber;
    // Prefer the explicit vendorName from qa005. Fall back to the legacy
    // "Client - Vendor" combined string for invoices created before
    // vendorName was a real column on the entity.
    if (inv.vendorName) {
      this.formClientName = inv.clientName ?? '';
      this.formVendorName = inv.vendorName;
    } else {
      const clientName = inv.clientName ?? '';
      const sep = clientName.indexOf(' - ');
      if (sep > 0) {
        this.formClientName = clientName.slice(0, sep);
        this.formVendorName = clientName.slice(sep + 3);
      } else {
        this.formClientName = clientName;
        this.formVendorName = '';
      }
    }
    this.formIssueDate = inv.issueDateUtc;
    this.formDueDate = inv.dueDateUtc;
    this.formPeriodStart = inv.periodStartUtc;
    this.formPeriodEnd = inv.periodEndUtc;
    this.formCurrency = inv.currency;
    this.formTaxRate = String(inv.taxRate);
    this.formNotes = inv.notes ?? '';
    // qa005 — hydrate Remit-to overrides. Empty string means "no
    // override; placeholder will show the branding default."
    this.formRemitBankName = inv.remitBankName ?? '';
    this.formRemitAccountNumber = inv.remitAccountNumber ?? '';
    this.formRemitRoutingNumber = inv.remitRoutingNumber ?? '';
    this.formRemitContactPhone = inv.remitContactPhone ?? '';
    this.formLineItems.set(
      inv.lineItems.length > 0
        ? inv.lineItems.map((li) => ({
            description: li.description,
            weekStart: li.weekStartUtc ?? '',
            daysWorked: String(li.daysWorked || 0),
            hoursPerDay: String(li.hoursPerDay || 0),
            rate: String(li.rate),
            notes: li.notes ?? '',
          }))
        : [emptyLineItem()],
    );
    this.formError.set(null);
  }

  protected startNewInvoice(): void {
    const today = isoToday();
    const monday = mondayOf(today);
    const periodEnd = isoOffsetDays(30); // 1 month default period
    const due = isoOffsetDays(15);
    this.selectedInvoiceId.set(null);
    this.formInvoiceNumber.set('');
    this.formInvoiceNumberInput = '';
    this.formClientName = '';
    this.formVendorName = '';
    this.formIssueDate = today;
    this.formDueDate = due;
    this.formPeriodStart = monday;
    this.formPeriodEnd = periodEnd;
    this.formCurrency = 'USD';
    this.formTaxRate = '0';
    this.formNotes = '';
    this.formRemitBankName = '';
    this.formRemitAccountNumber = '';
    this.formRemitRoutingNumber = '';
    this.formRemitContactPhone = '';
    this.formLineItems.set([{ ...emptyLineItem(), weekStart: monday }]);
    this.formError.set(null);
  }

  // ── Line items ───────────────────────────────────────────────────
  protected addLineItem(): void {
    // Default the next week to the Monday AFTER the latest one in the list,
    // so adding a "Week 2" naturally lines up with the contractor's billing.
    const list = this.formLineItems();
    const lastWeekStart = list.length > 0 ? list[list.length - 1].weekStart : '';
    const nextWeek = lastWeekStart ? isoAddDays(lastWeekStart, 7) : mondayOf(isoToday());
    const lastRate = list.length > 0 ? list[list.length - 1].rate : '0';
    this.formLineItems.update((cur) => [
      ...cur,
      { ...emptyLineItem(), weekStart: nextWeek, rate: lastRate },
    ]);
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

  /** Hours = Days × Hours/Day, rounded to 2dp. Used in the "5×8=40" display. */
  protected lineItemHours(item: DraftLineItem): number {
    const d = Number(item.daysWorked);
    const hpd = Number(item.hoursPerDay);
    if (!Number.isFinite(d) || !Number.isFinite(hpd) || d < 0 || hpd < 0) return 0;
    return Math.round(d * hpd * 100) / 100;
  }

  /** Amount = Hours × Rate, rounded to 2dp. */
  protected lineItemAmount(item: DraftLineItem): number {
    const r = Number(item.rate);
    if (!Number.isFinite(r) || r < 0) return 0;
    return Math.round(this.lineItemHours(item) * r * 100) / 100;
  }

  /** Sunday end-of-week computed from the Monday start. Empty for non-weekly. */
  protected lineItemWeekEnd(item: DraftLineItem): string {
    if (!item.weekStart) return '';
    const start = new Date(item.weekStart + 'T00:00:00Z');
    if (Number.isNaN(start.getTime())) return '';
    start.setUTCDate(start.getUTCDate() + 6);
    return start.toISOString().slice(0, 10);
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

  /**
   * Fetch the server-rendered PDF as a Blob and trigger a CSV download.
   * Mirrors downloadPdf() but with the /csv endpoint.
   */
  protected downloadCsv(): void {
    const invoice = this.selectedInvoice();
    if (!invoice) return;
    this.downloadingCsv.set(true);
    this.formError.set(null);

    this.svc.downloadCsv(invoice.id).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `Invoice-${invoice.invoiceNumber}.csv`;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        window.URL.revokeObjectURL(url);
        this.downloadingCsv.set(false);
      },
      error: (e: unknown) => {
        this.downloadingCsv.set(false);
        this.formError.set(toMessage(e));
      },
    });
  }

  /**
   * Fetch the rendered PDF and stuff it into an iframe via createObjectURL
   * so the contractor sees exactly what the client will receive. The Blob
   * URL is revoked when the modal closes (closePreview).
   */
  protected openPreview(): void {
    const invoice = this.selectedInvoice();
    if (!invoice) return;
    this.loadingPreview.set(true);
    this.formError.set(null);

    this.svc.downloadPdf(invoice.id).subscribe({
      next: (blob) => {
        // Revoke any previous URL before allocating a new one.
        const existing = this.previewUrl();
        if (existing) window.URL.revokeObjectURL(existing);
        const url = window.URL.createObjectURL(blob);
        this.previewUrl.set(url);
        this.loadingPreview.set(false);
      },
      error: (e: unknown) => {
        this.loadingPreview.set(false);
        this.formError.set(toMessage(e));
      },
    });
  }

  protected closePreview(): void {
    const url = this.previewUrl();
    if (url) window.URL.revokeObjectURL(url);
    this.previewUrl.set(null);
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
        Number(li.daysWorked) > 0 ||
        Number(li.hoursPerDay) > 0 ||
        Number(li.rate) > 0,
      )
      .map((li) => ({
        description: li.description.trim() || '—',
        weekStartUtc: li.weekStart || null,
        daysWorked: Number(li.daysWorked) || 0,
        hoursPerDay: Number(li.hoursPerDay) || 0,
        rate: Number(li.rate) || 0,
        notes: li.notes.trim() || null,
      }));

    if (items.length === 0) {
      return 'Add at least one line item before saving.';
    }
    for (const li of items) {
      if (li.daysWorked < 0 || li.hoursPerDay < 0 || li.rate < 0) {
        return 'Line item days, hours/day, and rate must be non-negative.';
      }
    }

    // Send Client and Vendor as separate fields now that qa005 added
    // VendorName as a first-class column. We keep clientName as the
    // raw client text only — the backend stores VendorName separately
    // and the PDF renderer concatenates them on the "Billed to" line.
    const client = this.formClientName.trim();
    const vendor = this.formVendorName.trim();

    return {
      invoiceNumber: this.formInvoiceNumberInput.trim() || null,
      clientName: client || null,
      issueDateUtc: issue,
      dueDateUtc: due,
      periodStartUtc: start,
      periodEndUtc: end,
      currency: this.formCurrency.trim().toUpperCase(),
      taxRate,
      lineItems: items,
      notes: this.formNotes.trim() || null,
      // qa005 — per-invoice overrides. null on the wire = inherit
      // tenant branding default at render time.
      remitBankName: this.formRemitBankName.trim() || null,
      remitAccountNumber: this.formRemitAccountNumber.trim() || null,
      remitRoutingNumber: this.formRemitRoutingNumber.trim() || null,
      remitContactPhone: this.formRemitContactPhone.trim() || null,
      vendorName: vendor || null,
    };
  }
}

// ── Local helpers ────────────────────────────────────────────────────

/**
 * Working shape for a line item in the form. Strings on every numeric so
 * empty inputs survive (Angular ngModel passes "" instead of NaN).
 * Each item represents one work-week (Monday → Sunday).
 */
interface DraftLineItem {
  description: string;
  weekStart: string;   // ISO yyyy-MM-dd of the Monday, or '' for non-weekly
  daysWorked: string;
  hoursPerDay: string;
  rate: string;
  notes: string;
}

function emptyLineItem(defaultRate?: number): DraftLineItem {
  return {
    description: '',
    weekStart: '',
    daysWorked: '5',
    hoursPerDay: '8',
    rate: defaultRate != null ? String(defaultRate) : '0',
    notes: '',
  };
}

function isoToday(): string {
  return new Date().toISOString().slice(0, 10);
}

function isoOffsetDays(days: number): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

/** Given any ISO date, returns the Monday of that week (UTC, ISO weekday). */
function mondayOf(iso: string): string {
  const d = new Date(iso + 'T00:00:00Z');
  if (Number.isNaN(d.getTime())) return iso;
  // getUTCDay: Sunday=0, Monday=1, ..., Saturday=6
  // Want Monday-based. Sunday → -6, Monday → 0, Tuesday → -1, etc.
  const dow = d.getUTCDay();
  const offset = dow === 0 ? -6 : 1 - dow;
  d.setUTCDate(d.getUTCDate() + offset);
  return d.toISOString().slice(0, 10);
}

function isoAddDays(iso: string, days: number): string {
  const d = new Date(iso + 'T00:00:00Z');
  if (Number.isNaN(d.getTime())) return iso;
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
