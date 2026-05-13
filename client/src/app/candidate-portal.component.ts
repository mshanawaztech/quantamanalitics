import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { MeService } from './core/auth/me.service';
import {
  CandidateProfile,
  CandidateProfileService,
  CandidateTimelineItem,
} from './core/candidate/candidate-profile.service';
import {
  QaAlertComponent,
  QaButtonComponent,
  QaEmptyStateComponent,
  QaInputComponent,
} from './core/ui';

/**
 * Mapping from backend event-type strings (loose match) to a stable visual
 * key + WCAG-compliant tone token consumed by the timeline rail. Keeping the
 * map file-scoped means the template never sees raw status strings or
 * hard-coded hex values, so the design system stays in one place.
 */
const TIMELINE_KIND_MAP = {
  applied: 'applied',
  interview: 'interview',
  offer: 'offer',
  hire: 'hired',
  reject: 'rejected',
} as const;

type TimelineKind =
  | (typeof TIMELINE_KIND_MAP)[keyof typeof TIMELINE_KIND_MAP]
  | 'note';

const TIMELINE_PREVIEW_LIMIT = 5;
const SUCCESS_TRANSIENT_MS = 4000;
const CONTRACTOR_ROLE = 'Contractor';

interface ProfileFormState {
  fullName: string;
  email: string;
  phoneNumber: string;
  headline: string;
  summary: string;
}

const EMPTY_FORM: ProfileFormState = {
  fullName: '',
  email: '',
  phoneNumber: '',
  headline: '',
  summary: '',
};

/**
 * PR-56 — candidate portal v2.
 *
 * Replaces the stub feel of /candidate (the dashboard) with a real
 * portal at /candidate/portal. Three sections:
 *   1. My applications  — recent timeline preview (5 events, color-coded).
 *   2. My profile        — read-only by default, inline edit form on demand.
 *   3. Quick actions     — link tiles, contractor tile gated on role claim.
 *
 * The dashboard route stays put so external bookmarks keep working — this
 * is additive. Portal nav now points at /candidate/portal, but the
 * breadcrumb label map still resolves both crumbs to "Candidate".
 */
@Component({
  selector: 'app-candidate-portal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    QaAlertComponent,
    QaButtonComponent,
    QaEmptyStateComponent,
    QaInputComponent,
  ],
  template: `
    <div class="page">
      <header class="page__header">
        <p class="eyebrow">Candidate portal</p>
        <h1>Welcome back{{ greetingName() ? ', ' + greetingName() : '' }}.</h1>
        <p class="lede">
          Track every application you've submitted, keep your profile sharp,
          and jump to the workflows you use most.
        </p>
      </header>

      <!-- ── 1. My applications ─────────────────────────────────── -->
      <section
        class="card"
        aria-labelledby="applications-heading"
      >
        <div class="card__head">
          <div>
            <h2 id="applications-heading">My applications</h2>
            <p class="card__sub">Most recent activity from your applications.</p>
          </div>
          <a
            class="card__link"
            [routerLink]="timelineAllRoute"
            data-testid="timeline-view-all"
          >View all</a>
        </div>

        @if (timelineLoading()) {
          <ol class="timeline timeline--skeleton" aria-label="Loading recent activity">
            @for (placeholder of skeletonRows; track placeholder) {
              <li class="timeline__item">
                <span class="timeline__dot" aria-hidden="true"></span>
                <div class="timeline__body">
                  <div class="skeleton skeleton--line skeleton--w-60"></div>
                  <div class="skeleton skeleton--line skeleton--w-30"></div>
                </div>
              </li>
            }
          </ol>
        } @else if (timelineError()) {
          <qa-alert tone="danger" title="Could not load activity">
            <p>{{ timelineError() }}</p>
            <qa-button variant="secondary" (click)="reloadTimeline()">Retry</qa-button>
          </qa-alert>
        } @else if (timelinePreview().length === 0) {
          <qa-empty-state
            title="No application activity yet"
            description="No application activity yet — apply to a job to get started."
            icon="·"
          >
            <a class="card__link" routerLink="/jobs">Browse open roles</a>
          </qa-empty-state>
        } @else {
          <ol class="timeline" data-testid="timeline-list">
            @for (event of timelinePreview(); track event.id) {
              <li class="timeline__item">
                <span
                  class="timeline__dot"
                  [attr.data-kind]="kindFor(event.eventType)"
                  aria-hidden="true"
                ></span>
                <div class="timeline__body">
                  <div class="timeline__row">
                    <strong class="timeline__title">{{ event.title }}</strong>
                    <span class="timeline__when">{{ relativeTime(event.occurredAtUtc) }}</span>
                  </div>
                  <p class="timeline__detail">
                    {{ event.detail || event.jobTitle || 'Workflow update' }}
                  </p>
                </div>
              </li>
            }
          </ol>
        }
      </section>

      <!-- ── 2. My profile ──────────────────────────────────────── -->
      <section
        #profileSection
        class="card"
        aria-labelledby="profile-heading"
      >
        @if (saveSuccess()) {
          <qa-alert tone="success" title="Profile saved">
            Your changes are live.
          </qa-alert>
        }

        <div class="card__head">
          <div>
            <h2 id="profile-heading">My profile</h2>
            <p class="card__sub">How recruiters see you across Quantam Analytics.</p>
          </div>
          @if (!editing() && profile()) {
            <qa-button variant="secondary" (click)="enterEdit()" data-testid="profile-edit">
              Edit profile
            </qa-button>
          }
        </div>

        @if (profileLoading() && !profile()) {
          <div class="profile-skeleton" aria-label="Loading profile">
            <div class="skeleton skeleton--line skeleton--w-40"></div>
            <div class="skeleton skeleton--line skeleton--w-60"></div>
            <div class="skeleton skeleton--line skeleton--w-50"></div>
          </div>
        } @else if (profileError() && !profile()) {
          <qa-alert tone="danger" title="Could not load profile">
            <p>{{ profileError() }}</p>
            <qa-button variant="secondary" (click)="reloadProfile()">Retry</qa-button>
          </qa-alert>
        } @else if (editing()) {
          @if (saveError()) {
            <qa-alert tone="danger" title="Could not save profile">
              {{ saveError() }}
            </qa-alert>
          }
          <form class="profile-form" (ngSubmit)="saveProfile()" data-testid="profile-form">
            <qa-input
              label="Full name"
              name="fullName"
              [value]="form().fullName"
              (valueChange)="updateForm('fullName', $event)"
            ></qa-input>
            <qa-input
              label="Email"
              type="email"
              name="email"
              [required]="true"
              [value]="form().email"
              (valueChange)="updateForm('email', $event)"
            ></qa-input>
            <qa-input
              label="Phone number"
              type="tel"
              name="phoneNumber"
              [value]="form().phoneNumber"
              (valueChange)="updateForm('phoneNumber', $event)"
            ></qa-input>
            <qa-input
              label="Headline"
              name="headline"
              [value]="form().headline"
              (valueChange)="updateForm('headline', $event)"
            ></qa-input>

            <div class="profile-form__field profile-form__field--wide">
              <label class="profile-form__label" for="profile-summary">Summary</label>
              <textarea
                id="profile-summary"
                class="profile-form__textarea"
                rows="4"
                name="summary"
                [(ngModel)]="summaryDraft"
              ></textarea>
            </div>

            <div class="profile-form__actions profile-form__field--wide">
              <qa-button type="submit" variant="primary" [disabled]="saving()">
                {{ saving() ? 'Saving…' : 'Save changes' }}
              </qa-button>
              <qa-button type="button" variant="ghost" (click)="cancelEdit()">
                Cancel
              </qa-button>
            </div>
          </form>
        } @else if (profile(); as p) {
          <dl class="profile-view" data-testid="profile-view">
            <div class="profile-view__row">
              <dt>Full name</dt>
              <dd>{{ p.fullName || '—' }}</dd>
            </div>
            <div class="profile-view__row">
              <dt>Email</dt>
              <dd>{{ p.email }}</dd>
            </div>
            <div class="profile-view__row">
              <dt>Phone number</dt>
              <dd>{{ p.phoneNumber || '—' }}</dd>
            </div>
            <div class="profile-view__row">
              <dt>Headline</dt>
              <dd>{{ p.headline || '—' }}</dd>
            </div>
            <div class="profile-view__row profile-view__row--wide">
              <dt>Summary</dt>
              <dd>{{ p.summary || '—' }}</dd>
            </div>
          </dl>
        }
      </section>

      <!-- ── 3. Quick actions ───────────────────────────────────── -->
      <section
        class="card"
        aria-labelledby="actions-heading"
      >
        <div class="card__head">
          <div>
            <h2 id="actions-heading">Quick actions</h2>
            <p class="card__sub">Jump straight to where you were headed.</p>
          </div>
        </div>

        <div class="tiles">
          <a class="tile" routerLink="/jobs">
            <span class="tile__icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="11" cy="11" r="7"></circle>
                <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
              </svg>
            </span>
            <strong class="tile__title">Browse jobs</strong>
            <span class="tile__desc">See every open role on the public board.</span>
          </a>

          <button class="tile tile--button" type="button" (click)="scrollToProfile()" data-testid="quick-update-profile">
            <span class="tile__icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M12 20h9"></path>
                <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z"></path>
              </svg>
            </span>
            <strong class="tile__title">Update profile</strong>
            <span class="tile__desc">Jump to your profile and keep details fresh.</span>
          </button>

          @if (showContractorTile()) {
            <a class="tile" routerLink="/contractor" data-testid="contractor-tile">
              <span class="tile__icon" aria-hidden="true">
                <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <rect x="3" y="4" width="18" height="16" rx="2"></rect>
                  <path d="M3 10h18"></path>
                  <path d="M8 2v4"></path>
                  <path d="M16 2v4"></path>
                </svg>
              </span>
              <strong class="tile__title">Submitted timesheet</strong>
              <span class="tile__desc">Open the contractor portal to review and submit hours.</span>
            </a>
          }
        </div>
      </section>
    </div>
  `,
  styles: `
    :host { display: block; }

    .page {
      display: grid;
      gap: var(--space-5);
      width: min(960px, 100%);
      margin: 0 auto;
    }

    .page__header { padding: var(--space-2) 0 var(--space-1); }
    .eyebrow {
      margin: 0 0 var(--space-2);
      color: var(--color-primary);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semi);
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    h1 {
      margin: 0 0 var(--space-2);
      font-size: var(--font-size-3xl);
      color: var(--color-ink-strong);
      line-height: 1.1;
    }
    .lede { margin: 0; color: var(--color-ink-muted); max-width: 56ch; }

    /* ── Card surface ─────────────────────────────────────────── */
    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: var(--space-5);
      display: grid;
      gap: var(--space-4);
    }
    .card__head {
      display: flex;
      justify-content: space-between;
      gap: var(--space-4);
      align-items: flex-start;
      flex-wrap: wrap;
    }
    .card__head h2 {
      margin: 0;
      font-size: var(--font-size-xl);
      color: var(--color-ink-strong);
    }
    .card__sub {
      margin: var(--space-1) 0 0;
      color: var(--color-ink-muted);
      font-size: var(--font-size-sm);
    }
    .card__link {
      color: var(--color-primary);
      font-weight: var(--font-weight-semi);
      font-size: var(--font-size-sm);
      text-decoration: none;
    }
    .card__link:hover { text-decoration: underline; }

    /* ── Timeline ─────────────────────────────────────────────── */
    .timeline {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: var(--space-4);
      position: relative;
    }
    .timeline::before {
      content: '';
      position: absolute;
      top: 6px;
      bottom: 6px;
      left: 5px;
      width: 2px;
      background: var(--color-primary-soft);
      border-radius: 1px;
    }
    .timeline__item {
      display: grid;
      grid-template-columns: 12px 1fr;
      gap: var(--space-3);
      align-items: flex-start;
    }
    .timeline__dot {
      width: 12px;
      height: 12px;
      border-radius: var(--radius-pill);
      background: var(--color-primary);
      margin-top: 4px;
      box-shadow: 0 0 0 3px var(--color-surface);
      position: relative;
      z-index: 1;
    }
    .timeline__dot[data-kind='hired']    { background: var(--color-success); }
    .timeline__dot[data-kind='rejected'] { background: var(--color-danger); }
    .timeline__dot[data-kind='offer']    { background: var(--color-warning); }

    .timeline__body { display: grid; gap: var(--space-1); min-width: 0; }
    .timeline__row {
      display: flex;
      justify-content: space-between;
      gap: var(--space-3);
      align-items: baseline;
      flex-wrap: wrap;
    }
    .timeline__title { color: var(--color-ink-strong); font-size: var(--font-size-md); }
    .timeline__when { color: var(--color-ink-muted); font-size: var(--font-size-xs); white-space: nowrap; }
    .timeline__detail { margin: 0; color: var(--color-ink-muted); font-size: var(--font-size-sm); }

    /* ── Skeleton (shimmer) ───────────────────────────────────── */
    .skeleton {
      background: linear-gradient(
        90deg,
        var(--color-surface-alt) 0%,
        var(--color-primary-soft) 50%,
        var(--color-surface-alt) 100%
      );
      background-size: 200% 100%;
      animation: qa-shimmer 1.4s ease-in-out infinite;
      border-radius: var(--radius-sm);
    }
    .skeleton--line { height: 0.85rem; margin-bottom: var(--space-2); }
    .skeleton--line:last-child { margin-bottom: 0; }
    .skeleton--w-30 { width: 30%; }
    .skeleton--w-40 { width: 40%; }
    .skeleton--w-50 { width: 50%; }
    .skeleton--w-60 { width: 60%; }

    @keyframes qa-shimmer {
      0%   { background-position: 200% 0; }
      100% { background-position: -200% 0; }
    }
    @media (prefers-reduced-motion: reduce) {
      .skeleton { animation: none; }
    }

    .profile-skeleton { display: grid; gap: var(--space-2); }

    /* ── Profile (read-only) ──────────────────────────────────── */
    .profile-view {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--space-4);
      margin: 0;
    }
    .profile-view__row { display: grid; gap: var(--space-1); margin: 0; }
    .profile-view__row--wide { grid-column: 1 / -1; }
    .profile-view dt {
      font-size: 13px;
      font-weight: var(--font-weight-medium);
      color: var(--color-ink-muted);
    }
    .profile-view dd {
      margin: 0;
      color: var(--color-ink-strong);
      font-size: var(--font-size-md);
      word-break: break-word;
    }

    /* ── Profile (edit form) ──────────────────────────────────── */
    .profile-form {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--space-4);
    }
    .profile-form__field { display: flex; flex-direction: column; gap: var(--space-2); }
    .profile-form__field--wide { grid-column: 1 / -1; }
    .profile-form__label {
      font-size: 13px;
      font-weight: var(--font-weight-medium);
      color: var(--color-ink-muted);
    }
    .profile-form__textarea {
      padding: var(--space-2) var(--space-3);
      background: var(--color-surface);
      border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-md);
      color: var(--color-ink);
      font: inherit;
      font-size: var(--font-size-md);
      resize: vertical;
      min-height: 6rem;
    }
    .profile-form__textarea:focus {
      outline: none;
      border-color: var(--color-primary);
      box-shadow: 0 0 0 3px var(--color-primary-ring);
    }
    .profile-form__actions {
      display: flex;
      gap: var(--space-3);
      align-items: center;
      flex-wrap: wrap;
    }

    /* ── Quick action tiles ──────────────────────────────────── */
    .tiles {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: var(--space-4);
    }
    .tile {
      padding: var(--space-5);
      border-radius: var(--radius-lg);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-ink);
      text-align: left;
      text-decoration: none;
      display: grid;
      gap: var(--space-2);
      align-content: start;
      cursor: pointer;
      font: inherit;
      transition: border-color 160ms ease, box-shadow 160ms ease, transform 160ms ease;
    }
    .tile:hover, .tile:focus-visible {
      border-color: var(--color-primary);
      box-shadow: var(--shadow-md);
      transform: translateY(-1px);
    }
    .tile--button { width: 100%; }
    .tile__icon {
      width: 2.5rem; height: 2.5rem;
      display: grid; place-items: center;
      background: var(--color-primary-soft);
      color: var(--color-primary);
      border-radius: var(--radius-md);
      margin-bottom: var(--space-2);
    }
    .tile__title {
      color: var(--color-ink-strong);
      font-weight: var(--font-weight-bold);
      font-size: var(--font-size-md);
    }
    .tile__desc {
      color: var(--color-ink-muted);
      font-size: var(--font-size-sm);
    }

    @media (max-width: 768px) {
      .profile-view, .profile-form, .tiles { grid-template-columns: 1fr; }
    }
  `,
})
export class CandidatePortalComponent {
  private candidateProfile = inject(CandidateProfileService);
  protected me = inject(MeService);

  protected readonly skeletonRows = [0, 1, 2] as const;
  protected readonly timelineAllRoute = '/candidate/dashboard';

  private profileSectionRef = viewChild<ElementRef<HTMLElement>>('profileSection');

  // Timeline state
  protected readonly timeline = signal<CandidateTimelineItem[]>([]);
  protected readonly timelineLoading = signal(true);
  protected readonly timelineError = signal<string | null>(null);

  // Profile state
  protected readonly profile = signal<CandidateProfile | null>(null);
  protected readonly profileLoading = signal(true);
  protected readonly profileError = signal<string | null>(null);

  // Edit / save state
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly saveSuccess = signal(false);
  protected readonly form = signal<ProfileFormState>(EMPTY_FORM);

  // Bound directly to the textarea via ngModel — signals would force a
  // two-way custom control, which qa-input doesn't expose. Using a plain
  // string keeps the textarea simple and the form state coherent.
  protected summaryDraft = '';

  protected readonly timelinePreview = computed(() =>
    this.timeline().slice(0, TIMELINE_PREVIEW_LIMIT),
  );

  protected readonly greetingName = computed(() => {
    const profile = this.profile();
    if (profile?.fullName) {
      return profile.fullName.split(' ')[0];
    }
    return null;
  });

  protected readonly showContractorTile = computed(() => {
    const roles = this.me.data()?.roles ?? [];
    return roles.includes(CONTRACTOR_ROLE);
  });

  private successTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.loadTimeline();
    this.loadProfile();
  }

  protected reloadTimeline(): void {
    this.loadTimeline();
  }

  protected reloadProfile(): void {
    this.loadProfile();
  }

  protected enterEdit(): void {
    const profile = this.profile();
    if (!profile) {
      return;
    }
    this.form.set(this.toFormState(profile));
    this.summaryDraft = profile.summary ?? '';
    this.saveError.set(null);
    this.editing.set(true);
  }

  protected cancelEdit(): void {
    this.editing.set(false);
    this.saveError.set(null);
    const profile = this.profile();
    if (profile) {
      this.form.set(this.toFormState(profile));
      this.summaryDraft = profile.summary ?? '';
    }
  }

  protected updateForm<K extends keyof ProfileFormState>(
    key: K,
    value: string | number,
  ): void {
    const next: ProfileFormState = { ...this.form(), [key]: String(value) };
    this.form.set(next);
  }

  protected saveProfile(): void {
    const current = this.form();
    this.saving.set(true);
    this.saveError.set(null);

    this.candidateProfile
      .save({
        email: current.email,
        fullName: current.fullName || null,
        phoneNumber: current.phoneNumber || null,
        headline: current.headline || null,
        summary: this.summaryDraft || null,
      })
      .subscribe({
        next: (saved) => {
          this.profile.set(saved);
          this.saving.set(false);
          this.editing.set(false);
          this.flashSuccess();
        },
        error: (error: unknown) => {
          this.saving.set(false);
          this.saveError.set(this.toErrorMessage(error));
        },
      });
  }

  protected scrollToProfile(): void {
    const el = this.profileSectionRef()?.nativeElement;
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  protected kindFor(eventType: string): TimelineKind {
    const normalized = eventType.toLowerCase();
    for (const [needle, kind] of Object.entries(TIMELINE_KIND_MAP)) {
      if (normalized.includes(needle)) {
        return kind;
      }
    }
    return 'note';
  }

  protected relativeTime(value: string): string {
    const then = new Date(value).getTime();
    if (Number.isNaN(then)) {
      return value;
    }
    const diffSec = Math.round((Date.now() - then) / 1000);
    const abs = Math.abs(diffSec);
    if (abs < 60) return 'just now';
    if (abs < 3600) return `${Math.round(diffSec / 60)} min ago`;
    if (abs < 86_400) return `${Math.round(diffSec / 3600)} h ago`;
    if (abs < 86_400 * 30) return `${Math.round(diffSec / 86_400)} d ago`;
    return new Date(value).toLocaleDateString();
  }

  private loadTimeline(): void {
    this.timelineLoading.set(true);
    this.timelineError.set(null);

    this.candidateProfile.timeline().subscribe({
      next: (response) => {
        this.timeline.set(response.items ?? []);
        this.timelineLoading.set(false);
      },
      error: (error: unknown) => {
        this.timelineError.set(this.toErrorMessage(error));
        this.timelineLoading.set(false);
      },
    });
  }

  private loadProfile(): void {
    this.profileLoading.set(true);
    this.profileError.set(null);

    this.candidateProfile.load().subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.form.set(this.toFormState(profile));
        this.summaryDraft = profile.summary ?? '';
        this.profileLoading.set(false);
      },
      error: (error: unknown) => {
        this.profileError.set(this.toErrorMessage(error));
        this.profileLoading.set(false);
      },
    });
  }

  private flashSuccess(): void {
    this.saveSuccess.set(true);
    if (this.successTimer !== null) {
      clearTimeout(this.successTimer);
    }
    this.successTimer = setTimeout(() => {
      this.saveSuccess.set(false);
      this.successTimer = null;
    }, SUCCESS_TRANSIENT_MS);
  }

  private toFormState(profile: CandidateProfile): ProfileFormState {
    return {
      fullName: profile.fullName ?? '',
      email: profile.email ?? '',
      phoneNumber: profile.phoneNumber ?? '',
      headline: profile.headline ?? '',
      summary: profile.summary ?? '',
    };
  }

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return (
        (error.error as { detail?: string; title?: string } | null)?.detail ??
        (error.error as { detail?: string; title?: string } | null)?.title ??
        error.message
      );
    }
    return error instanceof Error
      ? error.message
      : 'Unexpected candidate portal error.';
  }
}
