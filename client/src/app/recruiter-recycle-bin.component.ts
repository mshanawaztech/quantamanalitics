import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  RecycleBinJobItem,
  RecycleBinService,
} from './core/recruiter/recycle-bin.service';
import {
  QaAlertComponent,
  QaButtonComponent,
  QaEmptyStateComponent,
} from './core/ui';

/**
 * Track-B / consumes Story 61. The backend ships a 30-day restore window
 * for soft-deleted jobs and a 410-Gone response past it. This page is the
 * recruiter-facing surface — list of deleted jobs ordered by recency,
 * "Restore" affordance per row, and a window-status pill so the recruiter
 * sees at a glance which ones are close to permanent.
 *
 * No "permanently delete" affordance here on purpose: hard-delete is a
 * destructive op gated on the upcoming retention worker; the recycle bin
 * is the safe surface.
 */
@Component({
  selector: 'app-recruiter-recycle-bin',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [QaAlertComponent, QaButtonComponent, QaEmptyStateComponent],
  template: `
    <div class="page">
      <header class="page__header">
        <p class="eyebrow">Recruiter</p>
        <h1>Recycle bin</h1>
        <p class="lede">
          Deleted jobs can be restored for 30 days. After that the row stays
          in the database for audit but is no longer recoverable from this
          page.
        </p>
      </header>

      @if (loadError()) {
        <qa-alert tone="danger" role="alert">{{ loadError() }}</qa-alert>
      }
      @if (lastActionError()) {
        <qa-alert tone="danger" role="alert">{{ lastActionError() }}</qa-alert>
      }

      <section aria-labelledby="bin-heading">
        <header class="section__head">
          <h2 id="bin-heading">Deleted jobs</h2>
          @if (!loading()) {
            <p class="count">{{ items().length }} item{{ items().length === 1 ? '' : 's' }}</p>
          }
        </header>

        @if (loading()) {
          <p class="hint">Loading…</p>
        } @else if (items().length === 0) {
          <qa-empty-state
            title="Recycle bin is empty"
            hint="Delete a job from the active list and it'll land here for 30 days."
          />
        } @else {
          <ul class="rows">
            @for (job of items(); track job.id) {
              <li class="row" [class.row--expiring]="expiringSoon(job)">
                <div class="row__main">
                  <strong>{{ job.title }}</strong>
                  <small>{{ job.slug }}</small>
                </div>
                <div class="row__meta">
                  <span class="meta-label">Deleted</span>
                  <span>{{ relative(job.deletedAtUtc) }}</span>
                  @if (job.deletedByAuthSubject) {
                    <span class="meta-by">by {{ shortSubject(job.deletedByAuthSubject) }}</span>
                  }
                </div>
                <div class="row__window">
                  <span
                    class="window-pill"
                    [class.window-pill--warn]="expiringSoon(job)"
                  >{{ windowLabel(job) }}</span>
                </div>
                <div class="row__action">
                  <qa-button
                    variant="primary"
                    [disabled]="restoringId() === job.id"
                    (click)="restore(job)"
                  >{{ restoringId() === job.id ? 'Restoring…' : 'Restore' }}</qa-button>
                </div>
              </li>
            }
          </ul>
        }
      </section>
    </div>
  `,
  styles: `
    :host { display: block; }
    .page { width: min(1040px, 100%); margin: 0 auto; padding: 2rem 1.5rem 3rem; }
    .eyebrow { color: var(--color-primary, #1a3a8f); font-size: 12px; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; margin: 0 0 0.5rem; }
    h1 { margin: 0 0 0.5rem; font-size: 1.75rem; }
    .lede { color: var(--color-fg-muted, #5d6577); margin: 0 0 1.5rem; max-width: 60ch; }

    .section__head {
      display: flex; align-items: baseline; justify-content: space-between;
      gap: 1rem; margin-bottom: 0.75rem;
    }
    .section__head h2 { margin: 0; font-size: 1.125rem; }
    .count { color: var(--color-fg-muted, #5d6577); font-size: 0.875rem; margin: 0; }
    .hint { color: var(--color-fg-muted, #5d6577); }

    .rows { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.5rem; }
    .row {
      display: grid;
      grid-template-columns: 2fr 1.5fr auto auto;
      gap: 1rem;
      align-items: center;
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 10px;
      padding: 0.875rem 1rem;
    }
    .row--expiring { border-color: #c0392b; }

    .row__main { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .row__main strong { font-size: 1rem; }
    .row__main small { color: var(--color-fg-muted, #5d6577); font-family: ui-monospace, monospace; font-size: 12px; }

    .row__meta { display: flex; flex-direction: column; gap: 2px; color: var(--color-fg-muted, #5d6577); font-size: 0.875rem; }
    .meta-label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.08em; font-weight: 600; }
    .meta-by { font-size: 0.8rem; }

    .window-pill {
      display: inline-block;
      padding: 0.25rem 0.625rem;
      border-radius: 999px;
      background: var(--color-primary-soft, #e7ecf6);
      color: var(--color-primary, #1a3a8f);
      font-size: 0.75rem;
      font-weight: 600;
      white-space: nowrap;
    }
    .window-pill--warn { background: #fde8e4; color: #c0392b; }

    @media (max-width: 720px) {
      .row { grid-template-columns: 1fr; }
      .row__action { justify-self: start; }
    }
  `,
})
export class RecruiterRecycleBinComponent {
  private svc = inject(RecycleBinService);

  // 30 days, in milliseconds. The same window the backend enforces; we
  // duplicate it here only to power the visual countdown — the actual
  // restore validation happens server-side.
  private static readonly WindowMs = 30 * 24 * 60 * 60 * 1000;

  // Treat anything within 5 days of expiry as "expiring soon" so the
  // pill flips to a warning color before a recruiter loses the row.
  private static readonly ExpiryWarnMs = 5 * 24 * 60 * 60 * 1000;

  protected readonly items = signal<RecycleBinJobItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly lastActionError = signal<string | null>(null);
  protected readonly restoringId = signal<string | null>(null);

  protected readonly hasItems = computed(() => this.items().length > 0);

  constructor() {
    this.refresh();
  }

  protected refresh(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.svc.listJobs().subscribe({
      next: (r) => {
        this.items.set(r.items);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.loadError.set(this.toMessage(e));
        this.loading.set(false);
      },
    });
  }

  protected restore(job: RecycleBinJobItem): void {
    this.restoringId.set(job.id);
    this.lastActionError.set(null);

    this.svc.restoreJob(job.id).subscribe({
      next: () => {
        // Drop the restored row from the local list — it's no longer
        // deleted, so it doesn't belong on this page anymore.
        this.items.update((list) => list.filter((j) => j.id !== job.id));
        this.restoringId.set(null);
      },
      error: (e: unknown) => {
        this.restoringId.set(null);
        this.lastActionError.set(this.toMessage(e));
      },
    });
  }

  protected windowLabel(job: RecycleBinJobItem): string {
    const remainingMs = this.remainingMs(job);
    if (remainingMs <= 0) {
      return 'Out of window';
    }
    const days = Math.ceil(remainingMs / (24 * 60 * 60 * 1000));
    return days === 1 ? '1 day left' : `${days} days left`;
  }

  protected expiringSoon(job: RecycleBinJobItem): boolean {
    return this.remainingMs(job) <= RecruiterRecycleBinComponent.ExpiryWarnMs;
  }

  protected relative(value: string): string {
    const then = new Date(value).getTime();
    if (Number.isNaN(then)) return value;
    const diffSec = Math.round((Date.now() - then) / 1000);
    const abs = Math.abs(diffSec);
    if (abs < 60) return 'just now';
    if (abs < 3600) return `${Math.round(diffSec / 60)} min ago`;
    if (abs < 86_400) return `${Math.round(diffSec / 3600)} h ago`;
    if (abs < 86_400 * 30) return `${Math.round(diffSec / 86_400)} d ago`;
    return new Date(value).toLocaleDateString();
  }

  protected shortSubject(subject: string): string {
    // Auth0 subjects look like "auth0|abcdef" or "google-oauth2|xyz".
    // The recruiter rarely needs the provider tag, just the trailing id.
    const pipe = subject.indexOf('|');
    return pipe >= 0 ? subject.slice(pipe + 1) : subject;
  }

  private remainingMs(job: RecycleBinJobItem): number {
    const deletedAt = new Date(job.deletedAtUtc).getTime();
    if (Number.isNaN(deletedAt)) return 0;
    return deletedAt + RecruiterRecycleBinComponent.WindowMs - Date.now();
  }

  private toMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as { detail?: string; title?: string } | null;
      return body?.detail ?? body?.title ?? error.message;
    }
    return error instanceof Error ? error.message : 'Unexpected error.';
  }
}
