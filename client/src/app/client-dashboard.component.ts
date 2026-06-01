import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import {
  ClientApprovalService,
  ClientApprovalTimesheet,
} from './core/client/client-approval.service';
import { ClientInvoicesService } from './core/client/client-invoices.service';
import { InvoiceResponse, InvoiceStatus } from './core/contractor/invoices.service';
import {
  QaBadgeComponent,
  QaButtonComponent,
  QaCardComponent,
  QaPageHeadComponent,
} from './core/ui';

@Component({
  selector: 'app-client-dashboard',
  imports: [FormsModule, QaBadgeComponent, QaButtonComponent, QaCardComponent, QaPageHeadComponent],
  template: `
    <main class="page">
      <qa-page-head
        eyebrow="Client portal"
        title="Approve submitted work before payroll moves ahead."
        lede="Review submitted contractor time and invoices, approve clean weeks, and reject anything that needs correction with a visible note."
      >
        <qa-badge [tone]="accessBadgeTone()">{{ accessLabel() }}</qa-badge>
      </qa-page-head>

      @if (!auth.isAuthenticated()) {
        <qa-card>
          <h2 class="card-title">Client approval starts with sign in.</h2>
          <p class="muted">Use Auth0 to enter the protected approval area for submitted contractor time.</p>
          <qa-button variant="primary" (click)="auth.loginWithRedirect()">Sign in</qa-button>
        </qa-card>
      } @else if (!hasApprovalAccess()) {
        <qa-card>
          <h2 class="card-title">Approval access required.</h2>
          <p class="muted">
            Your current session is valid, but this workflow requires approval
            access such as Client, payroll admin, manager, or PlatformAdmin.
          </p>
        </qa-card>
      } @else {
        <section class="workspace">
          <qa-card>
            <header class="sec-head">
              <div>
                <p class="eyebrow">Submitted queue</p>
                <h2 class="card-title">Timesheets waiting on a decision</h2>
              </div>
              @if (loading()) {
                <qa-badge tone="neutral">Loading…</qa-badge>
              }
            </header>

            @if (error()) {
              <p class="error" role="alert" aria-live="assertive">{{ error() }}</p>
            } @else {
              <div class="rows">
                @for (timesheet of submittedTimesheets(); track timesheet.id) {
                  <article class="row">
                    <div class="row__head">
                      <div>
                        <strong>{{ timesheet.contractorEmail }}</strong>
                        <p class="muted">Week of {{ timesheet.weekStartUtc }} · {{ timesheet.totalHours }} hours</p>
                        <p class="muted">{{ totalsLabel(timesheet) }}</p>
                      </div>
                      <qa-badge tone="warning">{{ timesheet.status }}</qa-badge>
                    </div>

                    <div class="entries">
                      @for (entry of timesheet.entries; track entry.workDate + entry.entryType) {
                        <div class="entry-row">
                          <span>{{ entry.workDate }}</span>
                          <span>{{ entry.entryType }}</span>
                          <span>{{ entry.hours }}h</span>
                        </div>
                      }
                    </div>

                    <label class="field">
                      <span>Review note</span>
                      <textarea
                        rows="3"
                        [name]="'note-' + timesheet.id"
                        [(ngModel)]="reviewNotes[timesheet.id]"
                        placeholder="Required for rejection; optional for approval."
                      ></textarea>
                    </label>

                    <div class="actions">
                      <qa-button variant="primary" [disabled]="actingId() === timesheet.id" (click)="approve(timesheet)">
                        {{ actingId() === timesheet.id ? 'Saving…' : 'Approve' }}
                      </qa-button>
                      <qa-button variant="ghost" [disabled]="actingId() === timesheet.id" (click)="reject(timesheet)">
                        {{ actingId() === timesheet.id ? 'Saving…' : 'Reject' }}
                      </qa-button>
                    </div>
                  </article>
                } @empty {
                  <div class="empty">
                    <h3>No submitted timesheets right now</h3>
                    <p class="muted">Submitted contractor weeks will appear here for approval once the entry workflow is used.</p>
                  </div>
                }
              </div>
            }
          </qa-card>

          <qa-card>
            <header class="sec-head">
              <div>
                <p class="eyebrow">Recently reviewed</p>
                <h2 class="card-title">Approved or returned weeks</h2>
              </div>
            </header>
            <div class="rows rows--compact">
              @for (timesheet of reviewedTimesheets(); track timesheet.id) {
                <article class="row row--compact">
                  <div class="row__head">
                    <div>
                      <strong>{{ timesheet.contractorEmail }}</strong>
                      <p class="muted">Week of {{ timesheet.weekStartUtc }} · {{ totalsLabel(timesheet) }}</p>
                      @if (timesheet.reviewNote) {
                        <p class="note">{{ timesheet.reviewNote }}</p>
                      }
                    </div>
                    <qa-badge [tone]="timesheetTone(timesheet.status)">{{ timesheet.status }}</qa-badge>
                  </div>
                </article>
              } @empty {
                <p class="muted">No reviewed timesheets yet.</p>
              }
            </div>
          </qa-card>
        </section>

        <section class="workspace">
          <qa-card>
            <header class="sec-head">
              <div>
                <p class="eyebrow">Submitted queue</p>
                <h2 class="card-title">Invoices waiting on a decision</h2>
              </div>
              @if (invoicesLoading()) {
                <qa-badge tone="neutral">Loading…</qa-badge>
              }
            </header>

            @if (invoicesError()) {
              <p class="error" role="alert" aria-live="assertive">{{ invoicesError() }}</p>
            } @else {
              <div class="rows">
                @for (invoice of submittedInvoices(); track invoice.id) {
                  <article class="row">
                    <div class="row__head">
                      <div>
                        <strong>{{ invoice.invoiceNumber }}</strong>
                        <p class="muted">{{ invoice.contractorEmail }}</p>
                        <p class="muted">
                          {{ invoice.clientName || 'No client name' }} ·
                          Issued {{ invoice.issueDateUtc }} · Due {{ invoice.dueDateUtc }}
                        </p>
                        <p class="amount">{{ money(invoice) }}</p>
                      </div>
                      <qa-badge tone="warning">{{ invoice.status }}</qa-badge>
                    </div>

                    <div class="entries">
                      @for (line of invoice.lineItems; track line.id) {
                        <div class="entry-row entry-row--invoice">
                          <span>{{ line.description }}</span>
                          <span>{{ line.hours }}h × {{ line.rate }}</span>
                          <span>{{ invoice.currency }} {{ line.amount }}</span>
                        </div>
                      }
                    </div>

                    <label class="field">
                      <span>Review note</span>
                      <textarea
                        rows="3"
                        [name]="'invoice-note-' + invoice.id"
                        [(ngModel)]="invoiceNotes[invoice.id]"
                        placeholder="Required for rejection; optional for approval."
                      ></textarea>
                    </label>

                    <div class="actions">
                      <qa-button variant="primary" [disabled]="invoiceActingId() === invoice.id" (click)="approveInvoice(invoice)">
                        {{ invoiceActingId() === invoice.id ? 'Saving…' : 'Approve' }}
                      </qa-button>
                      <qa-button variant="ghost" [disabled]="invoiceActingId() === invoice.id" (click)="rejectInvoice(invoice)">
                        {{ invoiceActingId() === invoice.id ? 'Saving…' : 'Reject' }}
                      </qa-button>
                    </div>
                  </article>
                } @empty {
                  <div class="empty">
                    <h3>No submitted invoices right now</h3>
                    <p class="muted">When a contractor uses "Save and send", their invoice lands here for approval.</p>
                  </div>
                }
              </div>
            }
          </qa-card>

          <qa-card>
            <header class="sec-head">
              <div>
                <p class="eyebrow">Reviewed &amp; paid</p>
                <h2 class="card-title">Approved, rejected, or paid invoices</h2>
              </div>
            </header>
            <div class="rows rows--compact">
              @for (invoice of reviewedInvoices(); track invoice.id) {
                <article class="row row--compact">
                  <div class="row__head">
                    <div>
                      <strong>{{ invoice.invoiceNumber }}</strong>
                      <p class="muted">{{ money(invoice) }} · {{ invoice.contractorEmail }}</p>
                      @if (invoice.reviewerNote) {
                        <p class="note">{{ invoice.reviewerNote }}</p>
                      }
                    </div>
                    <qa-badge [tone]="invoiceTone(invoice.status)">{{ invoice.status }}</qa-badge>
                  </div>
                  @if (canMarkPaid() && invoice.status === 'Approved') {
                    <div class="actions">
                      <qa-button variant="primary" [disabled]="invoiceActingId() === invoice.id" (click)="markPaid(invoice)">
                        {{ invoiceActingId() === invoice.id ? 'Saving…' : 'Mark paid' }}
                      </qa-button>
                    </div>
                  }
                </article>
              } @empty {
                <p class="muted">No reviewed invoices yet.</p>
              }
            </div>
          </qa-card>
        </section>
      }
    </main>
  `,
  styles: `
    .page { width: min(1180px, 100%); margin: 0 auto; padding: 1.75rem 0 3rem; }

    .workspace {
      display: grid;
      grid-template-columns: minmax(0, 1.1fr) minmax(20rem, 0.9fr);
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
      margin-bottom: 0.85rem;
    }
    .eyebrow {
      margin: 0 0 0.25rem;
      color: var(--color-primary, #1a3a8f);
      text-transform: uppercase;
      letter-spacing: 0.06em;
      font-size: 0.74rem;
      font-weight: 800;
    }
    .card-title { margin: 0; font-size: 1.1rem; color: var(--color-ink-strong, #0d1b2a); }
    .muted { margin: 0.15rem 0 0; color: var(--color-ink-muted, #4b5a72); font-size: 0.88rem; }
    .note { margin: 0.4rem 0 0; font-style: italic; color: var(--color-ink-muted, #4b5a72); font-size: 0.85rem; }
    .amount {
      margin: 0.45rem 0 0;
      color: var(--color-primary, #1a3a8f);
      font-weight: 800;
      font-size: 1rem;
    }

    .rows { display: grid; gap: 0.8rem; }
    .rows--compact { gap: 0.55rem; }
    .row {
      padding: 1rem 1.1rem;
      border: 1px solid var(--color-border, #d8dee9);
      border-radius: 0.75rem;
      background: var(--color-surface, #fff);
    }
    .row--compact { padding: 0.8rem 0.95rem; }
    .row__head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      margin-bottom: 0.7rem;
    }
    .row--compact .row__head { margin-bottom: 0; }

    .entries { display: grid; gap: 0.4rem; margin-bottom: 0.9rem; }
    .entry-row {
      display: grid;
      grid-template-columns: 1.1fr 1fr auto;
      gap: 0.8rem;
      padding: 0.55rem 0.75rem;
      border-radius: 0.55rem;
      background: var(--color-surface-alt, #f0f3f9);
      color: var(--color-ink-muted, #4b5a72);
      font-size: 0.88rem;
    }
    .entry-row--invoice { grid-template-columns: 1.4fr 1fr auto; }

    .field { display: grid; gap: 0.3rem; }
    .field > span { color: var(--color-ink, #1a2942); font-weight: 600; font-size: 0.88rem; }
    .field textarea {
      width: 100%;
      padding: 0.7rem 0.85rem;
      border-radius: 0.6rem;
      border: 1px solid var(--color-border, #d8dee9);
      background: #fff;
      font: inherit;
    }
    .field textarea:focus {
      outline: 3px solid rgba(26,58,143,0.32);
      outline-offset: 1px;
      border-color: var(--color-primary, #1a3a8f);
    }

    .actions { display: flex; gap: 0.6rem; flex-wrap: wrap; margin-top: 0.9rem; }
    .empty {
      padding: 1.2rem;
      border: 1px dashed var(--color-border, #d8dee9);
      border-radius: 0.75rem;
      text-align: center;
    }
    .error { color: var(--color-danger, #b02a37); font-weight: 600; }

    @media (max-width: 980px) {
      .workspace { grid-template-columns: 1fr; }
    }
  `,
})
export class ClientDashboardComponent {
  protected auth = inject(AuthService);
  protected me = inject(MeService);
  protected access = inject(AccessService);
  private approvals = inject(ClientApprovalService);
  private invoiceApprovals = inject(ClientInvoicesService);

  protected items = signal<ClientApprovalTimesheet[]>([]);
  protected loading = signal(false);
  protected error = signal<string | null>(null);
  protected actingId = signal<string | null>(null);
  protected reviewNotes: Record<string, string> = {};

  protected invoices = signal<InvoiceResponse[]>([]);
  protected invoicesLoading = signal(false);
  protected invoicesError = signal<string | null>(null);
  protected invoiceActingId = signal<string | null>(null);
  protected invoiceNotes: Record<string, string> = {};

  constructor() {
    effect(() => {
      if (!this.hasApprovalAccess()) {
        return;
      }
      this.loadTimesheets();
      this.loadInvoices();
    });
  }

  protected hasApprovalAccess(): boolean {
    return this.access.canAccessTimeApproval();
  }

  protected accessLabel(): string {
    if (!this.auth.isAuthenticated()) return 'Awaiting sign in';
    return this.hasApprovalAccess() ? 'Approval workflow ready' : 'No approval access';
  }

  protected accessBadgeTone(): 'success' | 'warning' | 'neutral' {
    if (!this.auth.isAuthenticated()) return 'neutral';
    return this.hasApprovalAccess() ? 'success' : 'warning';
  }

  protected timesheetTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' {
    if (status === 'Approved') return 'success';
    if (status === 'Submitted') return 'warning';
    if (status === 'Rejected') return 'danger';
    return 'neutral';
  }

  protected invoiceTone(status: InvoiceStatus): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    if (status === 'Approved') return 'success';
    if (status === 'Submitted') return 'warning';
    if (status === 'Rejected') return 'danger';
    if (status === 'Paid') return 'info';
    return 'neutral';
  }

  protected submittedTimesheets(): ClientApprovalTimesheet[] {
    return this.items().filter((item) => item.status === 'Submitted');
  }

  protected reviewedTimesheets(): ClientApprovalTimesheet[] {
    return this.items().filter((item) => item.status !== 'Submitted');
  }

  protected totalsLabel(timesheet: ClientApprovalTimesheet): string {
    return `${timesheet.totals.regularHours} reg · ${timesheet.totals.overtimeHours} OT · ${timesheet.totals.paidTimeOffHours} PTO`;
  }

  protected approve(timesheet: ClientApprovalTimesheet): void {
    this.actingId.set(timesheet.id);
    this.error.set(null);

    this.approvals.approve(timesheet.id, this.reviewNotes[timesheet.id] || null).subscribe({
      next: (updated) => this.applyUpdatedTimesheet(updated),
      error: (error: unknown) => {
        this.actingId.set(null);
        this.error.set(this.toErrorMessage(error));
      },
    });
  }

  protected reject(timesheet: ClientApprovalTimesheet): void {
    const reason = (this.reviewNotes[timesheet.id] ?? '').trim();
    if (reason.length === 0) {
      this.error.set('Add a review note explaining why this timesheet is being rejected before sending it back.');
      return;
    }

    this.actingId.set(timesheet.id);
    this.error.set(null);

    this.approvals.reject(timesheet.id, reason).subscribe({
      next: (updated) => this.applyUpdatedTimesheet(updated),
      error: (error: unknown) => {
        this.actingId.set(null);
        this.error.set(this.toErrorMessage(error));
      },
    });
  }

  private loadTimesheets(): void {
    this.loading.set(true);
    this.error.set(null);

    this.approvals.timesheets().subscribe({
      next: (response) => {
        this.loading.set(false);
        this.items.set(response.items);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(this.toErrorMessage(error));
      },
    });
  }

  private applyUpdatedTimesheet(updated: ClientApprovalTimesheet): void {
    this.actingId.set(null);
    this.items.update((items) =>
      items.map((item) => item.id === updated.id ? updated : item),
    );
  }

  // ── Invoices ──────────────────────────────────────────────────────
  protected canMarkPaid(): boolean {
    return this.access.canAccessPayrollBilling();
  }

  protected submittedInvoices(): InvoiceResponse[] {
    return this.invoices().filter((invoice) => invoice.status === 'Submitted');
  }

  protected reviewedInvoices(): InvoiceResponse[] {
    return this.invoices().filter((invoice) => invoice.status !== 'Submitted');
  }

  protected money(invoice: InvoiceResponse): string {
    return `${invoice.currency} ${invoice.amount.toFixed(2)}`;
  }

  protected approveInvoice(invoice: InvoiceResponse): void {
    this.invoiceActingId.set(invoice.id);
    this.invoicesError.set(null);

    this.invoiceApprovals.approve(invoice.id, this.invoiceNotes[invoice.id] || null).subscribe({
      next: (updated) => this.applyUpdatedInvoice(updated),
      error: (error: unknown) => {
        this.invoiceActingId.set(null);
        this.invoicesError.set(this.toErrorMessage(error));
      },
    });
  }

  protected rejectInvoice(invoice: InvoiceResponse): void {
    const reason = (this.invoiceNotes[invoice.id] ?? '').trim();
    if (reason.length === 0) {
      this.invoicesError.set('Add a review note explaining why this invoice is being rejected before sending it back.');
      return;
    }

    this.invoiceActingId.set(invoice.id);
    this.invoicesError.set(null);

    this.invoiceApprovals.reject(invoice.id, reason).subscribe({
      next: (updated) => this.applyUpdatedInvoice(updated),
      error: (error: unknown) => {
        this.invoiceActingId.set(null);
        this.invoicesError.set(this.toErrorMessage(error));
      },
    });
  }

  protected markPaid(invoice: InvoiceResponse): void {
    this.invoiceActingId.set(invoice.id);
    this.invoicesError.set(null);

    this.invoiceApprovals.markPaid(invoice.id).subscribe({
      next: (updated) => this.applyUpdatedInvoice(updated),
      error: (error: unknown) => {
        this.invoiceActingId.set(null);
        this.invoicesError.set(this.toErrorMessage(error));
      },
    });
  }

  private loadInvoices(): void {
    this.invoicesLoading.set(true);
    this.invoicesError.set(null);

    this.invoiceApprovals.list().subscribe({
      next: (response) => {
        this.invoicesLoading.set(false);
        this.invoices.set(response.items);
      },
      error: (error: unknown) => {
        this.invoicesLoading.set(false);
        this.invoicesError.set(this.toErrorMessage(error));
      },
    });
  }

  private applyUpdatedInvoice(updated: InvoiceResponse): void {
    this.invoiceActingId.set(null);
    this.invoices.update((items) =>
      items.map((item) => (item.id === updated.id ? updated : item)),
    );
  }

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ?? error.error?.title ?? error.message;
    }
    return 'Unknown Error';
  }
}
