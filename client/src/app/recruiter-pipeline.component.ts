import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import {
  RecruiterApplication,
  RecruiterApplicationsBoard,
  RecruiterPortalService,
} from './core/recruiter/recruiter-portal.service';
import { QaAlertComponent, QaEmptyStateComponent } from './core/ui';

/**
 * PR-52 — recruiter pipeline kanban (`/recruiter/pipeline`).
 *
 * Five-column board (Applied → Interviewing → OfferSent → Hired → Rejected)
 * with HTML5 native drag-and-drop and a keyboard-accessible "Move…" menu.
 * Status changes are optimistic: the card moves locally first, then we
 * PATCH the API and roll back on failure with an inline alert. There is
 * no shared toast service yet, so the alert role + aria-live live on the
 * existing qa-alert primitive — TODO once core/ui ships a toast.
 *
 * Status enum values match the API's enum exactly, so the column id is
 * also the request payload value.
 */

type PipelineStatus =
  | 'Applied'
  | 'Interviewing'
  | 'OfferSent'
  | 'Hired'
  | 'Rejected';

interface PipelineColumn {
  id: PipelineStatus;
  label: string;
  description: string;
}

const COLUMNS: readonly PipelineColumn[] = [
  { id: 'Applied', label: 'Applied', description: 'New inbound applications waiting for triage.' },
  { id: 'Interviewing', label: 'Interviewing', description: 'Candidates in active interview loops.' },
  { id: 'OfferSent', label: 'Offer sent', description: 'Offers extended, awaiting response.' },
  { id: 'Hired', label: 'Hired', description: 'Candidates who accepted and start onboarding.' },
  { id: 'Rejected', label: 'Rejected', description: 'Closed-out applications, kept for history.' },
];

const STATUS_DOT_KIND: Record<PipelineStatus, string> = {
  Applied: 'applied',
  Interviewing: 'interview',
  OfferSent: 'offer',
  Hired: 'hired',
  Rejected: 'rejected',
};

@Component({
  selector: 'app-recruiter-pipeline',
  standalone: true,
  imports: [QaAlertComponent, QaEmptyStateComponent],
  template: `
    <main class="page">
      <header class="head">
        <p class="eyebrow">Recruiter portal</p>
        <h1>Pipeline</h1>
        <p class="lede">
          Drag cards between stages — or use each card's “Move…” menu — to
          update an application's status. Changes save automatically and
          undo if the server rejects them.
        </p>
      </header>

      @if (!auth.isAuthenticated()) {
        <section class="gate">
          <h2>Sign in to open the pipeline.</h2>
          <p>The pipeline is only available to authenticated recruiter accounts.</p>
          <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
        </section>
      } @else if (!hasRecruitingAccess()) {
        <section class="gate">
          <h2>Recruiter access required.</h2>
          <p>You're signed in, but this page is limited to Recruiter or PlatformAdmin roles.</p>
        </section>
      } @else {
        @if (errorMessage()) {
          <qa-alert tone="danger" title="We couldn't move that card">
            {{ errorMessage() }}
          </qa-alert>
        }

        @if (loading()) {
          <p class="loading" role="status">Loading pipeline…</p>
        }

        <section class="board" aria-label="Application pipeline">
          @for (column of columns; track column.id) {
            <section
              class="column"
              [class.column--target]="dragTarget() === column.id"
              [attr.data-status]="column.id"
              [attr.aria-label]="column.label + ' column'"
              (dragover)="onDragOver($event, column.id)"
              (dragleave)="onDragLeave($event, column.id)"
              (drop)="onDrop($event, column.id)"
            >
              <header class="column__head">
                <span class="dot" [attr.data-kind]="dotKind(column.id)" aria-hidden="true"></span>
                <h2>{{ column.label }}</h2>
                <span class="count" [attr.aria-label]="countLabel(column.id)">
                  {{ applicationsByStatus(column.id).length }}
                </span>
              </header>

              <p class="column__desc">{{ column.description }}</p>

              <div
                class="column__list"
                [attr.aria-label]="column.label + ' applications'"
              >
                @for (application of applicationsByStatus(column.id); track application.id) {
                  <article
                    class="card"
                    draggable="true"
                    [attr.aria-grabbed]="draggingId() === application.id || null"
                    [attr.data-application-id]="application.id"
                    (dragstart)="onDragStart($event, application)"
                    (dragend)="onDragEnd()"
                  >
                    <div class="card__top">
                      <strong class="card__name">{{ application.candidateName }}</strong>
                      <span class="dot dot--sm" [attr.data-kind]="dotKind(asStatus(application.status))" aria-hidden="true"></span>
                    </div>
                    <p class="card__job">{{ application.jobTitle }}</p>
                    <p class="card__meta">
                      <span>{{ daysInStageLabel(application) }}</span>
                    </p>
                    <div class="card__actions">
                      <button
                        type="button"
                        class="card__move"
                        [attr.aria-haspopup]="'menu'"
                        [attr.aria-expanded]="openMenuId() === application.id"
                        [attr.aria-controls]="'move-menu-' + application.id"
                        (click)="toggleMenu(application.id, $event)"
                      >
                        Move…
                      </button>
                      @if (openMenuId() === application.id) {
                        <ul
                          class="card__menu"
                          [attr.id]="'move-menu-' + application.id"
                          role="menu"
                          [attr.aria-label]="'Move ' + application.candidateName + ' to another stage'"
                        >
                          @for (target of otherColumns(application); track target.id) {
                            <li role="none">
                              <button
                                type="button"
                                role="menuitem"
                                (click)="moveFromMenu(application, target.id)"
                              >
                                {{ target.label }}
                              </button>
                            </li>
                          }
                        </ul>
                      }
                    </div>
                  </article>
                } @empty {
                  <qa-empty-state
                    [title]="'No candidates in this stage yet.'"
                    [description]="emptyDescription(column.id)"
                  />
                }
              </div>
            </section>
          }
        </section>
      }
    </main>
  `,
  styles: `
    :host { display: block; }
    .page { width: 100%; max-width: var(--container-max); margin: 0 auto; display: grid; gap: var(--space-5); }
    .head { display: grid; gap: var(--space-2); }
    .eyebrow {
      margin: 0; color: var(--color-primary);
      text-transform: uppercase; letter-spacing: 0.08em;
      font-size: var(--font-size-xs); font-weight: var(--font-weight-bold);
    }
    h1 { margin: 0; font-size: var(--font-size-3xl); color: var(--color-ink-strong); }
    .lede { margin: 0; color: var(--color-ink-muted); max-width: 60ch; line-height: var(--line-height-base); }
    .gate {
      background: var(--color-surface); border: 1px solid var(--color-border);
      border-radius: var(--radius-lg); padding: var(--space-6);
      display: grid; gap: var(--space-3); box-shadow: var(--shadow-sm);
    }
    .gate h2 { margin: 0; color: var(--color-ink-strong); }
    .gate p { margin: 0; color: var(--color-ink-muted); }
    .primary {
      justify-self: start; padding: var(--space-2) var(--space-4);
      background: var(--color-primary); color: var(--color-ink-onblue);
      border: 0; border-radius: var(--radius-md); font: inherit;
      font-weight: var(--font-weight-semi); cursor: pointer;
    }
    .primary:hover { background: var(--color-primary-hover); }
    .loading { margin: 0; color: var(--color-ink-muted); font-size: var(--font-size-sm); }
    .board {
      display: grid; grid-template-columns: repeat(5, minmax(15rem, 1fr));
      gap: var(--space-4); align-items: start; overflow-x: auto; padding-bottom: var(--space-2);
    }
    .column {
      background: var(--color-surface-alt); border: 1px solid var(--color-border);
      border-radius: var(--radius-lg); padding: var(--space-3);
      display: grid; gap: var(--space-3); align-content: start; min-height: 18rem;
      transition: background-color 160ms ease, border-color 160ms ease;
    }
    .column--target {
      background: var(--color-primary-soft); border-color: var(--color-primary);
      outline: 2px dashed var(--color-primary); outline-offset: -4px;
    }
    .column__head { display: flex; align-items: center; gap: var(--space-2); }
    .column__head h2 {
      margin: 0; flex: 1; font-size: var(--font-size-md);
      color: var(--color-ink-strong); font-weight: var(--font-weight-semi);
    }
    .count {
      font-size: var(--font-size-xs); color: var(--color-ink-muted);
      background: var(--color-surface); border: 1px solid var(--color-border);
      border-radius: var(--radius-pill); padding: 2px var(--space-2);
      min-width: 1.5rem; text-align: center;
    }
    .column__desc { margin: 0; font-size: var(--font-size-xs); color: var(--color-ink-muted); }
    .column__list { display: grid; gap: var(--space-2); }
    .card {
      background: var(--color-surface); border: 1px solid var(--color-border);
      border-radius: var(--radius-md); padding: var(--space-3);
      display: grid; gap: var(--space-2); cursor: grab; position: relative;
      transition: box-shadow 160ms ease, transform 160ms ease;
    }
    .card:hover { box-shadow: var(--shadow-md); border-color: var(--color-border-strong); transform: translateY(-1px); }
    .card:active { cursor: grabbing; }
    .card[aria-grabbed='true'] { opacity: 0.55; }
    .card__top { display: flex; align-items: center; gap: var(--space-2); }
    .card__name {
      flex: 1; color: var(--color-ink-strong); font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semi); overflow-wrap: anywhere;
    }
    .card__job { margin: 0; color: var(--color-ink); font-size: var(--font-size-sm); overflow-wrap: anywhere; }
    .card__meta { margin: 0; color: var(--color-ink-muted); font-size: var(--font-size-xs); }
    .card__actions { position: relative; }
    .card__move {
      padding: var(--space-1) var(--space-3); background: var(--color-surface-alt);
      color: var(--color-ink-strong); border: 1px solid var(--color-border);
      border-radius: var(--radius-pill); font: inherit;
      font-size: var(--font-size-xs); font-weight: var(--font-weight-semi); cursor: pointer;
    }
    .card__move:hover { background: var(--color-primary-soft); border-color: var(--color-primary); color: var(--color-primary); }
    .card__move:focus-visible { outline: none; box-shadow: 0 0 0 3px var(--color-primary-ring); }
    .card__menu {
      list-style: none; margin: var(--space-1) 0 0; padding: var(--space-1);
      background: var(--color-surface); border: 1px solid var(--color-border);
      border-radius: var(--radius-md); box-shadow: var(--shadow-md);
      position: absolute; top: 100%; left: 0; z-index: 5; min-width: 11rem;
    }
    .card__menu li { margin: 0; }
    .card__menu button {
      display: block; width: 100%; padding: var(--space-2) var(--space-3);
      text-align: left; background: transparent; border: 0;
      border-radius: var(--radius-sm); color: var(--color-ink);
      font: inherit; font-size: var(--font-size-sm); cursor: pointer;
    }
    .card__menu button:hover,
    .card__menu button:focus-visible {
      background: var(--color-primary-soft); color: var(--color-primary-hover); outline: none;
    }
    .dot {
      width: 0.6rem; height: 0.6rem; border-radius: var(--radius-pill);
      background: var(--color-border-strong); flex-shrink: 0;
    }
    .dot--sm { width: 0.5rem; height: 0.5rem; }
    .dot[data-kind='applied']   { background: #2563eb; }
    .dot[data-kind='interview'] { background: #7c3aed; }
    .dot[data-kind='offer']     { background: #d97706; }
    .dot[data-kind='hired']     { background: var(--color-success); }
    .dot[data-kind='rejected']  { background: var(--color-danger); }
    @media (max-width: 1100px) { .board { grid-template-columns: repeat(3, minmax(15rem, 1fr)); } }
    @media (max-width: 720px) { .board { grid-template-columns: 1fr; } }
  `,
})
export class RecruiterPipelineComponent {
  protected readonly auth = inject(AuthService);
  private readonly me = inject(MeService);
  private readonly recruiter = inject(RecruiterPortalService);

  protected readonly columns = COLUMNS;

  protected readonly applications = signal<RecruiterApplication[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  /** The application currently being dragged, or null. */
  protected readonly draggingId = signal<string | null>(null);
  /** The column the cursor is currently hovering during a drag. */
  protected readonly dragTarget = signal<PipelineStatus | null>(null);
  /** Which card has its "Move…" menu open. */
  protected readonly openMenuId = signal<string | null>(null);

  protected readonly hasData = computed(() => this.applications().length > 0);

  constructor() {
    effect(() => {
      if (!this.hasRecruitingAccess()) {
        return;
      }
      this.fetchApplications();
    });
  }

  protected hasRecruitingAccess(): boolean {
    const roles = this.me.data()?.roles ?? [];
    return roles.includes('Recruiter') || roles.includes('PlatformAdmin');
  }

  protected applicationsByStatus(status: PipelineStatus): RecruiterApplication[] {
    return this.applications().filter((a) => a.status === status);
  }

  protected otherColumns(application: RecruiterApplication): readonly PipelineColumn[] {
    return this.columns.filter((c) => c.id !== application.status);
  }

  protected dotKind(status: PipelineStatus): string {
    return STATUS_DOT_KIND[status];
  }

  protected asStatus(value: string): PipelineStatus {
    // The API enum mirrors PipelineStatus exactly. If a tenant somehow
    // returns an unknown value we fall back to "Applied" so the dot still
    // renders rather than blanking.
    return (STATUS_DOT_KIND as Record<string, string>)[value]
      ? (value as PipelineStatus)
      : 'Applied';
  }

  protected countLabel(status: PipelineStatus): string {
    const n = this.applicationsByStatus(status).length;
    return `${n} ${n === 1 ? 'application' : 'applications'} in ${status}`;
  }

  protected emptyDescription(status: PipelineStatus): string {
    if (status === 'Applied') {
      return 'New applications will land here as candidates apply to open roles.';
    }
    if (status === 'Rejected') {
      return 'Closed-out applications appear here for historical reference.';
    }
    return 'Drag a card here, or use a card’s “Move…” menu to bring candidates into this stage.';
  }

  protected daysInStageLabel(application: RecruiterApplication): string {
    // updatedAtUtc is the most recent status mutation; appliedAtUtc is the
    // initial intake. We prefer updated for "days in stage" since that's
    // what recruiters care about when triaging.
    const since = new Date(application.updatedAtUtc || application.appliedAtUtc);
    const now = new Date();
    const ms = now.getTime() - since.getTime();
    const days = Math.max(0, Math.floor(ms / (1000 * 60 * 60 * 24)));
    if (days === 0) {
      return 'Today';
    }
    return `${days} ${days === 1 ? 'day' : 'days'} in stage`;
  }

  protected toggleMenu(applicationId: string, event: Event): void {
    event.stopPropagation();
    this.openMenuId.update((current) => (current === applicationId ? null : applicationId));
  }

  protected moveFromMenu(application: RecruiterApplication, target: PipelineStatus): void {
    this.openMenuId.set(null);
    this.changeStatus(application, target);
  }

  // ── Drag and drop ──────────────────────────────────────────────────

  protected onDragStart(event: DragEvent, application: RecruiterApplication): void {
    this.draggingId.set(application.id);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', application.id);
    }
  }

  protected onDragEnd(): void {
    this.draggingId.set(null);
    this.dragTarget.set(null);
  }

  protected onDragOver(event: DragEvent, target: PipelineStatus): void {
    // preventDefault is required for a drop event to fire on this element.
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
    if (this.dragTarget() !== target) {
      this.dragTarget.set(target);
    }
  }

  protected onDragLeave(event: DragEvent, target: PipelineStatus): void {
    // dragleave fires when crossing into a child element too; only clear
    // the target when we leave the column outline itself.
    const related = event.relatedTarget as Node | null;
    const column = event.currentTarget as HTMLElement | null;
    if (column && related && column.contains(related)) {
      return;
    }
    if (this.dragTarget() === target) {
      this.dragTarget.set(null);
    }
  }

  protected onDrop(event: DragEvent, target: PipelineStatus): void {
    event.preventDefault();
    this.dragTarget.set(null);

    const id = event.dataTransfer?.getData('text/plain') ?? this.draggingId();
    this.draggingId.set(null);
    if (!id) {
      return;
    }

    const application = this.applications().find((a) => a.id === id);
    if (!application) {
      return;
    }

    this.changeStatus(application, target);
  }

  // ── State updates ──────────────────────────────────────────────────

  /**
   * Optimistic move: update local state first so the UI feels instant,
   * then PATCH the API. On failure, restore the original status and
   * surface a danger alert. The server-returned application replaces the
   * placeholder so updatedAtUtc / status are authoritative on success.
   */
  private changeStatus(application: RecruiterApplication, target: PipelineStatus): void {
    if (application.status === target) {
      return;
    }

    const previousStatus = application.status;
    const optimistic: RecruiterApplication = {
      ...application,
      status: target,
      updatedAtUtc: new Date().toISOString(),
    };

    this.applications.update((items) =>
      items.map((item) => (item.id === application.id ? optimistic : item)),
    );
    this.errorMessage.set(null);

    this.recruiter.moveApplication(application.id, target).subscribe({
      next: (updated) => {
        this.applications.update((items) =>
          items.map((item) => (item.id === updated.id ? updated : item)),
        );
      },
      error: (error: unknown) => {
        // Roll back optimistic move so the UI matches server reality.
        this.applications.update((items) =>
          items.map((item) =>
            item.id === application.id ? { ...item, status: previousStatus } : item,
          ),
        );
        this.errorMessage.set(this.toErrorMessage(error));
      },
    });
  }

  private fetchApplications(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.recruiter.applications().subscribe({
      next: (board: RecruiterApplicationsBoard) => {
        this.applications.set(board.items);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.errorMessage.set(this.toErrorMessage(error));
      },
    });
  }

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ?? error.error?.title ?? error.message;
    }
    return error instanceof Error ? error.message : 'Could not update the application status.';
  }
}
