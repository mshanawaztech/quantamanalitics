import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import {
  ClientApprovalService,
  ClientApprovalTimesheet,
} from './core/client/client-approval.service';

@Component({
  selector: 'app-client-dashboard',
  imports: [FormsModule],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Client approval</p>
          <h1>Approve submitted weeks before payroll and invoices move ahead.</h1>
          <p>
            This portal gives client-side approvers a narrow workflow: review
            submitted contractor time, approve clean weeks, and reject anything
            that needs correction with a visible note.
          </p>
        </div>
        <div class="hero-card">
          <p class="label">Approval access</p>
          <strong>{{ accessLabel() }}</strong>
          <span>{{ roleLabel() }}</span>
        </div>
      </section>

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Client approval starts with sign in.</h2>
          <p>Use Auth0 to enter the protected approval area for submitted contractor time.</p>
          <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
        </section>
      } @else if (!hasApprovalAccess()) {
        <section class="gate-card">
          <h2>Approval access required.</h2>
          <p>Your current session is valid, but this workflow is limited to Client or PlatformAdmin roles.</p>
        </section>
      } @else {
        <section class="workspace">
          <article class="queue-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Submitted queue</p>
                <h2>Timesheets waiting on a decision</h2>
              </div>
              @if (loading()) {
                <span class="pill">Loading</span>
              }
            </div>

            @if (error()) {
              <p class="error">{{ error() }}</p>
            } @else {
              <div class="queue-list">
                @for (timesheet of submittedTimesheets(); track timesheet.id) {
                  <article class="timesheet-card">
                    <div class="timesheet-head">
                      <div>
                        <strong>{{ timesheet.contractorEmail }}</strong>
                        <p>Week of {{ timesheet.weekStartUtc }} · {{ timesheet.totalHours }} hours</p>
                      </div>
                      <span class="pill">{{ timesheet.status }}</span>
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

                    <label>
                      Review note
                      <textarea
                        rows="3"
                        [name]="'note-' + timesheet.id"
                        [(ngModel)]="reviewNotes[timesheet.id]"
                        placeholder="Required for rejection; optional for approval."
                      ></textarea>
                    </label>

                    <div class="actions">
                      <button type="button" class="primary" (click)="approve(timesheet)" [disabled]="actingId() === timesheet.id">
                        {{ actingId() === timesheet.id ? 'Saving…' : 'Approve' }}
                      </button>
                      <button type="button" class="secondary" (click)="reject(timesheet)" [disabled]="actingId() === timesheet.id">
                        {{ actingId() === timesheet.id ? 'Saving…' : 'Reject' }}
                      </button>
                    </div>
                  </article>
                } @empty {
                  <article class="empty-card">
                    <h3>No submitted timesheets right now</h3>
                    <p>Submitted contractor weeks will appear here for approval once the entry workflow is used.</p>
                  </article>
                }
              </div>
            }
          </article>

          <aside class="history-card">
            <p class="eyebrow">Recently reviewed</p>
            <h2>Approved or returned weeks stay visible.</h2>
            <div class="history-list">
              @for (timesheet of reviewedTimesheets(); track timesheet.id) {
                <article class="history-item">
                  <strong>{{ timesheet.contractorEmail }}</strong>
                  <p>{{ timesheet.status }} · Week of {{ timesheet.weekStartUtc }}</p>
                  @if (timesheet.reviewNote) {
                    <p>{{ timesheet.reviewNote }}</p>
                  }
                </article>
              } @empty {
                <p class="empty">No reviewed timesheets yet.</p>
              }
            </div>
          </aside>
        </section>
      }
    </main>
  `,
  styles: `
    .page { width: min(1180px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero, .workspace { display: grid; gap: 1.25rem; }
    .hero { grid-template-columns: 1.15fr 0.85fr; margin-bottom: 1.5rem; }
    .workspace { grid-template-columns: minmax(0, 1.1fr) minmax(20rem, 0.9fr); align-items: start; }
    .hero-card, .gate-card, .queue-card, .history-card, .timesheet-card, .empty-card, .history-item {
      padding: 1.6rem;
      border-radius: 1.5rem;
      background: rgb(255 251 244 / 0.88);
      border: 1px solid rgb(87 70 42 / 0.14);
      box-shadow: 0 1rem 2rem rgb(64 47 22 / 0.06);
    }
    .eyebrow { margin: 0 0 0.7rem; color: #9a3412; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.8rem; font-size: clamp(2.1rem, 3.8vw, 4.2rem); line-height: 0.98; }
    h2, h3 { margin: 0; }
    p { color: #554d41; line-height: 1.7; }
    .label { margin: 0 0 0.4rem; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; color: #9a3412; font-weight: 800; }
    .hero-card strong { display: block; font-size: 1.2rem; margin-bottom: 0.45rem; }
    .section-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1rem; }
    .pill {
      padding: 0.35rem 0.7rem;
      border-radius: 999px;
      background: #e7e5e4;
      color: #44403c;
      font-size: 0.85rem;
      font-weight: 700;
    }
    .queue-list, .history-list { display: grid; gap: 0.9rem; }
    .timesheet-card, .history-item, .empty-card { background: #fffdf9; }
    .timesheet-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 0.9rem; }
    .entries { display: grid; gap: 0.45rem; margin-bottom: 0.9rem; }
    .entry-row {
      display: grid;
      grid-template-columns: 1.1fr 1fr auto;
      gap: 0.8rem;
      padding: 0.65rem 0.8rem;
      border-radius: 0.85rem;
      background: #f7f2e8;
      color: #554d41;
    }
    label { display: grid; gap: 0.35rem; color: #3f372c; font-weight: 600; }
    textarea {
      width: 100%;
      padding: 0.85rem 0.95rem;
      border-radius: 0.9rem;
      border: 1px solid #d8c8b0;
      background: #fffdf9;
      font: inherit;
    }
    .actions { display: flex; gap: 0.8rem; flex-wrap: wrap; margin-top: 0.9rem; }
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
    .empty, .error { color: #7c6f5e; }
    .error { color: #b91c1c; font-weight: 600; }
    @media (max-width: 980px) {
      .hero, .workspace, .entry-row { grid-template-columns: 1fr; }
      .timesheet-head { flex-direction: column; }
    }
  `,
})
export class ClientDashboardComponent {
  protected auth = inject(AuthService);
  protected me = inject(MeService);
  private approvals = inject(ClientApprovalService);

  protected items = signal<ClientApprovalTimesheet[]>([]);
  protected loading = signal(false);
  protected error = signal<string | null>(null);
  protected actingId = signal<string | null>(null);
  protected reviewNotes: Record<string, string> = {};

  constructor() {
    effect(() => {
      if (!this.hasApprovalAccess()) {
        return;
      }

      this.loadTimesheets();
    });
  }

  protected hasApprovalAccess(): boolean {
    const roles = this.me.data()?.roles ?? [];
    return roles.includes('Client') || roles.includes('PlatformAdmin');
  }

  protected accessLabel(): string {
    if (!this.auth.isAuthenticated()) {
      return 'Awaiting sign in';
    }

    return this.hasApprovalAccess() ? 'Approval workflow ready' : 'Authenticated without approval access';
  }

  protected roleLabel(): string {
    return this.me.data()?.roles.join(', ') || 'Role not yet available';
  }

  protected submittedTimesheets(): ClientApprovalTimesheet[] {
    return this.items().filter((item) => item.status === 'Submitted');
  }

  protected reviewedTimesheets(): ClientApprovalTimesheet[] {
    return this.items().filter((item) => item.status !== 'Submitted');
  }

  protected approve(timesheet: ClientApprovalTimesheet): void {
    this.actingId.set(timesheet.id);
    this.error.set(null);

    this.approvals.approve(timesheet.id, this.reviewNotes[timesheet.id] || null).subscribe({
      next: (updated) => {
        this.applyUpdatedTimesheet(updated);
      },
      error: (error: unknown) => {
        this.actingId.set(null);
        this.error.set(this.toErrorMessage(error));
      },
    });
  }

  protected reject(timesheet: ClientApprovalTimesheet): void {
    this.actingId.set(timesheet.id);
    this.error.set(null);

    this.approvals.reject(timesheet.id, this.reviewNotes[timesheet.id] || '').subscribe({
      next: (updated) => {
        this.applyUpdatedTimesheet(updated);
      },
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

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ?? error.error?.title ?? error.message;
    }

    return 'Unknown Error';
  }
}
