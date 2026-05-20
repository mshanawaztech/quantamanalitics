import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import {
  RecruiterApplication,
  RecruiterApplicationFilterPreset,
  RecruiterApplicationsBoard,
  RecruiterApplicationsQuery,
  RecruiterPortalService,
} from './core/recruiter/recruiter-portal.service';
import { QaAlertComponent, QaEmptyStateComponent } from './core/ui';

type PipelineStatus =
  | 'Applied'
  | 'Interviewing'
  | 'OfferSent'
  | 'Hired'
  | 'Rejected';

type PipelineFilterStatus = PipelineStatus | 'All';

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
  imports: [FormsModule, QaAlertComponent, QaEmptyStateComponent],
  template: `
    <main class="page">
      <header class="head">
        <p class="eyebrow">Recruiter portal</p>
        <h1>Pipeline</h1>
        <p class="lede">
          Search the board, save recruiter-specific views, tag candidate cards,
          and run bulk actions without leaving the live pipeline.
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
          <p>
            You're signed in, but this page requires recruiting access such as
            Recruiter, HR admin, manager, or PlatformAdmin.
          </p>
        </section>
      } @else {
        @if (errorMessage()) {
          <qa-alert tone="danger" title="Recruiter pipeline action failed">
            {{ errorMessage() }}
          </qa-alert>
        }

        <section class="controls">
          <form class="filters" (ngSubmit)="applyFilters()">
            <div class="controls__head">
              <div>
                <p class="eyebrow">Search and filter</p>
                <h2>Focus the board without losing the pipeline context</h2>
              </div>
              @if (loading()) {
                <span class="pill">Loading</span>
              }
            </div>

            <label>
              Search
              <input
                type="text"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
                name="search"
                placeholder="Name, email, role, location, skills, or tags"
              />
            </label>

            <label>
              Stage
              <select
                [ngModel]="statusFilter()"
                (ngModelChange)="statusFilter.set($event)"
                name="status"
              >
                <option value="All">All stages</option>
                @for (column of columns; track column.id) {
                  <option [value]="column.id">{{ column.label }}</option>
                }
              </select>
            </label>

            <label>
              Tag
              <select
                [ngModel]="tagFilter()"
                (ngModelChange)="tagFilter.set($event)"
                name="tag"
              >
                <option value="">All tags</option>
                @for (tag of availableTags(); track tag) {
                  <option [value]="tag">{{ tag }}</option>
                }
              </select>
            </label>

            <label>
              Location
              <select
                [ngModel]="locationFilter()"
                (ngModelChange)="locationFilter.set($event)"
                name="location"
              >
                <option value="">All locations</option>
                @for (location of availableLocations(); track location) {
                  <option [value]="location">{{ location }}</option>
                }
              </select>
            </label>

            <label class="checkbox">
              <input
                type="checkbox"
                [ngModel]="stuckOnly()"
                (ngModelChange)="stuckOnly.set($event)"
                name="stuckOnly"
              />
              Only show candidates stuck longer than 7 days
            </label>

            <div class="actions full-width">
              <button type="submit" class="primary" [disabled]="loading()">
                {{ loading() ? 'Refreshing…' : 'Apply filters' }}
              </button>
              <button type="button" class="secondary" (click)="resetFilters()" [disabled]="loading()">
                Reset
              </button>
              <span class="hint">{{ applications().length }} cards in current view</span>
            </div>
          </form>

          <section class="saved">
            <div class="controls__head">
              <div>
                <p class="eyebrow">Saved views</p>
                <h2>Reuse common recruiter board presets</h2>
              </div>
            </div>

            <label>
              Saved filter
              <select
                [ngModel]="activePresetId()"
                (ngModelChange)="applySavedFilter($event)"
                name="savedPreset"
              >
                <option value="">Choose a saved view</option>
                @for (preset of savedFilters(); track preset.id) {
                  <option [value]="preset.id">{{ preset.name }}</option>
                }
              </select>
            </label>

            <label>
              Save current filters as
              <input
                type="text"
                [ngModel]="presetName()"
                (ngModelChange)="presetName.set($event)"
                name="presetName"
                placeholder="Urgent cloud intake"
              />
            </label>

            <div class="actions">
              <button type="button" class="primary" (click)="savePreset()" [disabled]="savingPreset()">
                {{ savingPreset() ? 'Saving…' : 'Save preset' }}
              </button>
              <button
                type="button"
                class="secondary"
                (click)="deletePreset()"
                [disabled]="!activePresetId() || savingPreset()"
              >
                Delete preset
              </button>
            </div>
          </section>
        </section>

        <section class="bulk">
          <div class="bulk__head">
            <div>
              <p class="eyebrow">Bulk actions</p>
              <h2>Move or retag the cards you've selected</h2>
            </div>
            <span class="pill">{{ selectedCount() }} selected</span>
          </div>

          <div class="bulk__controls">
            <button type="button" class="secondary" (click)="selectVisible()" [disabled]="applications().length === 0">
              Select visible
            </button>
            <button type="button" class="secondary" (click)="clearSelection()" [disabled]="selectedCount() === 0">
              Clear selection
            </button>

            <label>
              Bulk stage
              <select
                [ngModel]="bulkMoveStatus()"
                (ngModelChange)="bulkMoveStatus.set($event)"
                name="bulkMoveStatus"
              >
                @for (column of columns; track column.id) {
                  <option [value]="column.id">{{ column.label }}</option>
                }
              </select>
            </label>

            <button
              type="button"
              class="primary"
              (click)="bulkMoveSelected()"
              [disabled]="selectedCount() === 0 || bulkActionPending()"
            >
              {{ bulkActionPending() ? 'Working…' : 'Move selected' }}
            </button>

            <label class="bulk__tag">
              Tags
              <input
                type="text"
                [ngModel]="bulkTagText()"
                (ngModelChange)="bulkTagText.set($event)"
                name="bulkTagText"
                placeholder="urgent, referred, backend"
              />
            </label>

            <button
              type="button"
              class="secondary"
              (click)="bulkTagSelected('Add')"
              [disabled]="selectedCount() === 0 || bulkActionPending()"
            >
              Add tags
            </button>
            <button
              type="button"
              class="secondary"
              (click)="bulkTagSelected('Remove')"
              [disabled]="selectedCount() === 0 || bulkActionPending()"
            >
              Remove tags
            </button>
          </div>
        </section>

        @if (loading()) {
          <p class="loading" role="status">Loading pipeline…</p>
        }

        <section class="board" aria-label="Application pipeline">
          @for (column of visibleColumns(); track column.id) {
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

              <div class="column__list" [attr.aria-label]="column.label + ' applications'">
                @for (application of applicationsByStatus(column.id); track application.id) {
                  <article
                    class="card"
                    draggable="true"
                    [attr.aria-grabbed]="draggingId() === application.id || null"
                    [attr.data-application-id]="application.id"
                    [attr.data-selected]="isSelected(application.id) ? 'true' : null"
                    (dragstart)="onDragStart($event, application)"
                    (dragend)="onDragEnd()"
                  >
                    <label class="card__select">
                      <input
                        type="checkbox"
                        [checked]="isSelected(application.id)"
                        (change)="toggleSelection(application.id, $any($event.target).checked)"
                      />
                      <span>Selected</span>
                    </label>

                    <div class="card__top">
                      <strong class="card__name">{{ application.candidateName }}</strong>
                      <span class="dot dot--sm" [attr.data-kind]="dotKind(asStatus(application.status))" aria-hidden="true"></span>
                    </div>
                    <p class="card__job">{{ application.jobTitle }}</p>
                    <p class="card__meta">
                      <span>{{ daysInStageLabel(application) }}</span>
                      @if (application.isStuck) {
                        <span class="stuck-chip">Stuck &gt; 7d</span>
                      }
                    </p>

                    @if (application.tags.length > 0) {
                      <div class="tags" aria-label="Application tags">
                        @for (tag of application.tags; track tag) {
                          <span class="tag">{{ tag }}</span>
                        }
                      </div>
                    }

                    @if (application.note) {
                      <p class="card__note">{{ application.note }}</p>
                    }

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
    h2 { margin: 0; color: var(--color-ink-strong); }
    .lede { margin: 0; color: var(--color-ink-muted); max-width: 62ch; line-height: var(--line-height-base); }
    .gate {
      background: var(--color-surface); border: 1px solid var(--color-border);
      border-radius: var(--radius-lg); padding: var(--space-6);
      display: grid; gap: var(--space-3); box-shadow: var(--shadow-sm);
    }
    .gate h2 { margin: 0; color: var(--color-ink-strong); }
    .gate p { margin: 0; color: var(--color-ink-muted); }
    .controls {
      display: grid;
      grid-template-columns: minmax(0, 1.7fr) minmax(20rem, 1fr);
      gap: var(--space-4);
    }
    .filters,
    .saved,
    .bulk {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: var(--space-4);
      box-shadow: var(--shadow-sm);
      display: grid;
      gap: var(--space-3);
    }
    .filters {
      grid-template-columns: repeat(2, minmax(0, 1fr));
      align-items: end;
    }
    .controls__head,
    .bulk__head {
      display: flex;
      justify-content: space-between;
      gap: var(--space-3);
      align-items: start;
      grid-column: 1 / -1;
    }
    .controls__head h2,
    .bulk__head h2 {
      font-size: var(--font-size-lg);
    }
    .pill {
      border: 1px solid var(--color-border);
      background: var(--color-surface-alt);
      color: var(--color-ink-muted);
      border-radius: var(--radius-pill);
      padding: var(--space-1) var(--space-2);
      font-size: var(--font-size-xs);
      white-space: nowrap;
    }
    .filters label,
    .saved label,
    .bulk label {
      display: grid;
      gap: var(--space-1);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semi);
      color: var(--color-ink-strong);
    }
    .full-width { grid-column: 1 / -1; }
    input[type='text'],
    select,
    textarea {
      width: 100%;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: var(--space-2) var(--space-3);
      background: var(--color-surface);
      color: var(--color-ink-strong);
      font: inherit;
    }
    input[type='text']:focus-visible,
    select:focus-visible,
    textarea:focus-visible {
      outline: none;
      box-shadow: 0 0 0 3px var(--color-primary-ring);
      border-color: var(--color-primary);
    }
    .checkbox {
      display: flex !important;
      align-items: center;
      gap: var(--space-2);
      font-weight: var(--font-weight-regular) !important;
      color: var(--color-ink);
    }
    .checkbox input {
      width: auto;
      accent-color: var(--color-primary);
    }
    .actions,
    .bulk__controls {
      display: flex;
      gap: var(--space-2);
      flex-wrap: wrap;
      align-items: center;
    }
    .hint {
      color: var(--color-ink-muted);
      font-size: var(--font-size-xs);
    }
    .primary,
    .secondary,
    .card__move {
      border-radius: var(--radius-pill);
      font: inherit;
      font-weight: var(--font-weight-semi);
      cursor: pointer;
      padding: var(--space-2) var(--space-4);
    }
    .primary {
      background: var(--color-primary);
      color: var(--color-ink-onblue);
      border: 0;
    }
    .primary:hover { background: var(--color-primary-hover); }
    .secondary,
    .card__move {
      background: var(--color-surface-alt);
      color: var(--color-ink-strong);
      border: 1px solid var(--color-border);
    }
    .secondary:hover,
    .card__move:hover {
      background: var(--color-primary-soft);
      border-color: var(--color-primary);
      color: var(--color-primary);
    }
    .bulk__tag { min-width: 16rem; flex: 1; }
    .saved { align-content: start; }
    .bulk { display: grid; gap: var(--space-3); }
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
    .card[data-selected='true'] {
      border-color: var(--color-primary);
      box-shadow: 0 0 0 2px var(--color-primary-ring);
    }
    .card__select {
      display: flex;
      align-items: center;
      gap: var(--space-2);
      font-size: var(--font-size-xs);
      color: var(--color-ink-muted);
    }
    .card__select input { accent-color: var(--color-primary); }
    .card__top { display: flex; align-items: center; gap: var(--space-2); }
    .card__name {
      flex: 1; color: var(--color-ink-strong); font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semi); overflow-wrap: anywhere;
    }
    .card__job,
    .card__note { margin: 0; color: var(--color-ink); font-size: var(--font-size-sm); overflow-wrap: anywhere; }
    .card__meta { margin: 0; color: var(--color-ink-muted); font-size: var(--font-size-xs); display: flex; gap: var(--space-2); flex-wrap: wrap; }
    .stuck-chip {
      border-radius: var(--radius-pill);
      background: rgba(185, 28, 28, 0.12);
      color: var(--color-danger);
      padding: 2px var(--space-2);
      font-weight: var(--font-weight-semi);
    }
    .tags {
      display: flex;
      gap: var(--space-1);
      flex-wrap: wrap;
    }
    .tag {
      border-radius: var(--radius-pill);
      background: var(--color-primary-soft);
      color: var(--color-primary);
      padding: 2px var(--space-2);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semi);
    }
    .card__actions { position: relative; }
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
    .dot[data-kind='interview'] { background: #4f46e5; }
    .dot[data-kind='offer']     { background: #0891b2; }
    .dot[data-kind='hired']     { background: var(--color-success); }
    .dot[data-kind='rejected']  { background: var(--color-danger); }
    @media (max-width: 1100px) {
      .controls { grid-template-columns: 1fr; }
      .board { grid-template-columns: repeat(3, minmax(15rem, 1fr)); }
    }
    @media (max-width: 720px) {
      .filters { grid-template-columns: 1fr; }
      .board { grid-template-columns: 1fr; }
    }
  `,
})
export class RecruiterPipelineComponent {
  protected readonly auth = inject(AuthService);
  private readonly me = inject(MeService);
  private readonly access = inject(AccessService);
  private readonly recruiter = inject(RecruiterPortalService);

  protected readonly columns = COLUMNS;

  protected readonly applications = signal<RecruiterApplication[]>([]);
  protected readonly availableTags = signal<string[]>([]);
  protected readonly availableLocations = signal<string[]>([]);
  protected readonly savedFilters = signal<RecruiterApplicationFilterPreset[]>([]);

  protected readonly loading = signal(false);
  protected readonly bulkActionPending = signal(false);
  protected readonly savingPreset = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly draggingId = signal<string | null>(null);
  protected readonly dragTarget = signal<PipelineStatus | null>(null);
  protected readonly openMenuId = signal<string | null>(null);
  protected readonly selectedIds = signal<string[]>([]);

  protected readonly searchTerm = signal('');
  protected readonly statusFilter = signal<PipelineFilterStatus>('All');
  protected readonly tagFilter = signal('');
  protected readonly locationFilter = signal('');
  protected readonly stuckOnly = signal(false);
  protected readonly activePresetId = signal('');
  protected readonly presetName = signal('');
  protected readonly bulkMoveStatus = signal<PipelineStatus>('Interviewing');
  protected readonly bulkTagText = signal('');

  protected readonly selectedCount = computed(() => this.selectedIds().length);
  protected readonly visibleColumns = computed(() => {
    const filter = this.statusFilter();
    return filter === 'All' ? this.columns : this.columns.filter((column) => column.id === filter);
  });

  constructor() {
    effect(() => {
      if (!this.hasRecruitingAccess()) {
        return;
      }

      this.fetchApplications();
    });
  }

  protected hasRecruitingAccess(): boolean {
    return this.access.canAccessRecruitingWorkspace();
  }

  protected applicationsByStatus(status: PipelineStatus): RecruiterApplication[] {
    return this.applications().filter((application) => application.status === status);
  }

  protected otherColumns(application: RecruiterApplication): readonly PipelineColumn[] {
    return this.columns.filter((column) => column.id !== application.status);
  }

  protected dotKind(status: PipelineStatus): string {
    return STATUS_DOT_KIND[status];
  }

  protected asStatus(value: string): PipelineStatus {
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

  protected toggleSelection(applicationId: string, selected: boolean): void {
    this.selectedIds.update((current) => {
      if (selected) {
        return current.includes(applicationId) ? current : [...current, applicationId];
      }

      return current.filter((id) => id !== applicationId);
    });
  }

  protected isSelected(applicationId: string): boolean {
    return this.selectedIds().includes(applicationId);
  }

  protected selectVisible(): void {
    this.selectedIds.set(this.applications().map((application) => application.id));
  }

  protected clearSelection(): void {
    this.selectedIds.set([]);
  }

  protected applyFilters(): void {
    this.activePresetId.set('');
    this.fetchApplications();
  }

  protected resetFilters(): void {
    this.searchTerm.set('');
    this.statusFilter.set('All');
    this.tagFilter.set('');
    this.locationFilter.set('');
    this.stuckOnly.set(false);
    this.activePresetId.set('');
    this.presetName.set('');
    this.clearSelection();
    this.fetchApplications();
  }

  protected applySavedFilter(presetId: string): void {
    this.activePresetId.set(presetId);

    if (!presetId) {
      return;
    }

    const preset = this.savedFilters().find((item) => item.id === presetId);
    if (!preset) {
      return;
    }

    this.presetName.set(preset.name);
    this.searchTerm.set(preset.search ?? '');
    this.statusFilter.set((preset.status as PipelineFilterStatus | null) ?? 'All');
    this.tagFilter.set(preset.tag ?? '');
    this.locationFilter.set(preset.location ?? '');
    this.stuckOnly.set(preset.stuckOnly);
    this.fetchApplications();
  }

  protected savePreset(): void {
    const name = this.presetName().trim();
    if (!name) {
      this.errorMessage.set('Give the filter preset a short name before saving it.');
      return;
    }

    this.savingPreset.set(true);
    this.errorMessage.set(null);

    this.recruiter.saveApplicationFilterPreset({
      presetId: this.activePresetId() || null,
      name,
      search: this.searchTerm().trim() || null,
      status: this.statusFilter() === 'All' ? null : this.statusFilter(),
      tag: this.tagFilter().trim() || null,
      location: this.locationFilter().trim() || null,
      stuckOnly: this.stuckOnly(),
    }).subscribe({
      next: (preset) => {
        this.activePresetId.set(preset.id);
        this.presetName.set(preset.name);
        this.savingPreset.set(false);
        this.fetchApplications();
      },
      error: (error: unknown) => {
        this.savingPreset.set(false);
        this.errorMessage.set(this.toErrorMessage(error));
      },
    });
  }

  protected deletePreset(): void {
    const presetId = this.activePresetId();
    if (!presetId) {
      return;
    }

    this.savingPreset.set(true);
    this.errorMessage.set(null);

    this.recruiter.deleteApplicationFilterPreset(presetId).subscribe({
      next: () => {
        this.activePresetId.set('');
        this.presetName.set('');
        this.savingPreset.set(false);
        this.fetchApplications();
      },
      error: (error: unknown) => {
        this.savingPreset.set(false);
        this.errorMessage.set(this.toErrorMessage(error));
      },
    });
  }

  protected bulkMoveSelected(): void {
    if (this.selectedCount() === 0) {
      return;
    }

    this.bulkActionPending.set(true);
    this.errorMessage.set(null);

    this.recruiter.bulkMoveApplications({
      applicationIds: this.selectedIds(),
      status: this.bulkMoveStatus(),
    }).subscribe({
      next: () => {
        this.bulkActionPending.set(false);
        this.clearSelection();
        this.fetchApplications();
      },
      error: (error: unknown) => {
        this.bulkActionPending.set(false);
        this.errorMessage.set(this.toErrorMessage(error));
      },
    });
  }

  protected bulkTagSelected(operation: 'Add' | 'Remove'): void {
    const tags = this.bulkTagText()
      .split(',')
      .map((tag) => tag.trim())
      .filter((tag) => tag.length > 0);

    if (this.selectedCount() === 0 || tags.length === 0) {
      this.errorMessage.set('Select at least one card and enter one or more comma-separated tags.');
      return;
    }

    this.bulkActionPending.set(true);
    this.errorMessage.set(null);

    this.recruiter.bulkUpdateApplicationTags({
      applicationIds: this.selectedIds(),
      tags,
      operation,
    }).subscribe({
      next: () => {
        this.bulkActionPending.set(false);
        this.bulkTagText.set('');
        this.fetchApplications();
      },
      error: (error: unknown) => {
        this.bulkActionPending.set(false);
        this.errorMessage.set(this.toErrorMessage(error));
      },
    });
  }

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
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }

    if (this.dragTarget() !== target) {
      this.dragTarget.set(target);
    }
  }

  protected onDragLeave(event: DragEvent, target: PipelineStatus): void {
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

    const application = this.applications().find((item) => item.id === id);
    if (!application) {
      return;
    }

    this.changeStatus(application, target);
  }

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
      next: () => {
        this.fetchApplications();
      },
      error: (error: unknown) => {
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

    this.recruiter.applications(this.currentQuery()).subscribe({
      next: (board: RecruiterApplicationsBoard) => {
        this.applications.set(board.items);
        this.availableTags.set(board.availableTags);
        this.availableLocations.set(board.availableLocations);
        this.savedFilters.set(board.savedFilters);
        this.selectedIds.update((current) =>
          current.filter((id) => board.items.some((item) => item.id === id)),
        );
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.errorMessage.set(this.toErrorMessage(error));
      },
    });
  }

  private currentQuery(): RecruiterApplicationsQuery {
    return {
      search: this.searchTerm().trim() || null,
      status: this.statusFilter() === 'All' ? null : this.statusFilter(),
      tag: this.tagFilter().trim() || null,
      location: this.locationFilter().trim() || null,
      stuckOnly: this.stuckOnly(),
    };
  }

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ?? error.error?.title ?? error.message;
    }

    return error instanceof Error ? error.message : 'Could not update the recruiter pipeline.';
  }
}
