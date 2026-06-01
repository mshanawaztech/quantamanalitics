import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { AuthService } from './core/auth/auth.service';
import {
  ConsultantType,
  OnboardingItem,
  OnboardingPhase,
  OnboardingService,
} from './core/onboarding/onboarding.service';
import {
  QaBadgeComponent,
  QaButtonComponent,
  QaCardComponent,
  QaPageHeadComponent,
  QaProgressComponent,
} from './core/ui';

interface PhaseGroup {
  phase: OnboardingPhase;
  label: string;
  items: OnboardingItem[];
}

const PHASE_ORDER: { phase: OnboardingPhase; label: string }[] = [
  { phase: 'IdentityEligibility', label: 'Identity & eligibility' },
  { phase: 'ProfessionalBackground', label: 'Professional background' },
  { phase: 'Agreements', label: 'Agreements' },
  { phase: 'Screening', label: 'Screening' },
  { phase: 'PayrollLogistics', label: 'Payroll & logistics' },
  { phase: 'General', label: 'Other' },
];

@Component({
  selector: 'app-onboarding-consultant',
  imports: [
    QaBadgeComponent,
    QaButtonComponent,
    QaCardComponent,
    QaPageHeadComponent,
    QaProgressComponent,
  ],
  template: `
    <main class="page">
      <qa-page-head
        eyebrow="Your onboarding"
        title="Let's get you ready to work."
        lede="Complete each step below. We'll review what you submit and let you know if anything needs a fix."
      ></qa-page-head>

      @if (!auth.isAuthenticated()) {
        <qa-card>
          <h2 class="card-title">Sign in to start onboarding.</h2>
          <qa-button variant="primary" (click)="auth.loginWithRedirect()">Sign in</qa-button>
        </qa-card>
      } @else if (error()) {
        <qa-card><p class="error">{{ error() }}</p></qa-card>
      } @else if (total() === 0 && !loading()) {
        <qa-card>
          <h2 class="card-title">Start your onboarding</h2>
          <p class="muted">Pick what best describes your engagement and we'll build your checklist.</p>
          <div class="type-row">
            @for (t of types; track t.value) {
              <button
                type="button"
                class="type-card"
                [class.selected]="selectedType() === t.value"
                (click)="selectedType.set(t.value)"
              >
                <strong>{{ t.label }}</strong>
                <span>{{ t.blurb }}</span>
              </button>
            }
          </div>
          <qa-button variant="primary" [disabled]="applying()" (click)="begin()">
            {{ applying() ? 'Setting up…' : 'Begin onboarding' }}
          </qa-button>
        </qa-card>
      } @else {
        <div class="progress-row">
          <div class="progress-row__copy">
            <strong>{{ percent() }}%</strong>
            <span>{{ completed() }} of {{ total() }} steps done</span>
          </div>
          <qa-progress [value]="percent()" />
        </div>

        @for (group of groups(); track group.phase) {
          <qa-card>
            <header class="phase-head">
              <h2 class="card-title">{{ group.label }}</h2>
              <qa-badge [tone]="phaseTone(group)">{{ phaseLabel(group) }}</qa-badge>
            </header>
            <div class="steps">
              @for (item of group.items; track item.id) {
                <article class="step" [class.step--done]="item.status === 'Approved'">
                  <div class="step__main">
                    <div class="step__text">
                      <strong>
                        {{ item.title }}
                        @if (!item.isRequired) { <span class="optional">Optional</span> }
                      </strong>
                      @if (item.instructions) { <p class="muted">{{ item.instructions }}</p> }
                      @if (item.status === 'Approved' && item.reviewerNote) {
                        <p class="note">Reviewer: {{ item.reviewerNote }}</p>
                      }
                    </div>
                    <qa-badge [tone]="statusTone(item.status)">{{ statusLabel(item.status) }}</qa-badge>
                  </div>

                  @if (item.status === 'Pending') {
                    <div class="step__action">
                      <input
                        type="text"
                        [value]="notes[item.id] || ''"
                        (input)="setNote(item.id, $event)"
                        placeholder="Add a note (optional)"
                      />
                      <qa-button variant="primary" [disabled]="actingId() === item.id" (click)="submit(item)">
                        {{ actingId() === item.id ? 'Submitting…' : 'Mark done' }}
                      </qa-button>
                    </div>
                  }
                </article>
              }
            </div>
          </qa-card>
        }
      }
    </main>
  `,
  styles: `
    .page { width: min(900px, 100%); margin: 0 auto; padding: 1.75rem 0 3rem; }

    qa-card { display: block; margin-bottom: 1.1rem; }
    .card-title { margin: 0 0 0.5rem; font-size: 1.1rem; color: var(--color-ink-strong, #0d1b2a); }
    .muted { margin: 0.2rem 0 0; color: var(--color-ink-muted, #4b5a72); }

    .progress-row {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 1rem;
      align-items: center;
      padding: 1.1rem 1.25rem;
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dee9);
      border-radius: 0.75rem;
      box-shadow: var(--shadow-md);
      margin-bottom: 1.1rem;
    }
    .progress-row__copy { display: grid; }
    .progress-row__copy strong {
      font-size: 1.6rem;
      color: var(--color-primary, #1a3a8f);
      line-height: 1;
    }
    .progress-row__copy span { font-size: 0.85rem; color: var(--color-ink-muted, #4b5a72); }

    .phase-head {
      display: flex; justify-content: space-between; align-items: center;
      gap: 1rem; margin-bottom: 0.85rem;
    }

    .steps { display: grid; gap: 0.7rem; }
    .step {
      padding: 0.95rem 1.1rem;
      border: 1px solid var(--color-border, #d8dee9);
      border-radius: 0.75rem;
      background: var(--color-surface, #fff);
    }
    .step--done { background: #f5fbf7; }
    .step__main {
      display: flex; justify-content: space-between; gap: 1rem;
      align-items: flex-start;
    }
    .step__text strong { display: block; color: var(--color-ink-strong, #0d1b2a); }
    .step__text p { font-size: 0.9rem; }
    .note { font-style: italic; color: var(--color-ink-muted, #4b5a72); }
    .optional {
      font-size: 0.68rem; font-weight: 800;
      color: #475569; background: var(--color-surface-alt, #f0f3f9);
      padding: 0.1rem 0.45rem; border-radius: 999px;
      margin-left: 0.4rem; vertical-align: middle;
      letter-spacing: 0.04em; text-transform: uppercase;
    }

    .step__action {
      display: flex; gap: 0.7rem; margin-top: 0.9rem;
    }
    .step__action input {
      flex: 1; padding: 0.65rem 0.8rem;
      border-radius: 0.6rem;
      border: 1px solid var(--color-border, #d8dee9);
      font: inherit;
      background: #fff;
    }
    .step__action input:focus {
      outline: 3px solid rgba(26,58,143,0.32);
      outline-offset: 1px;
      border-color: var(--color-primary, #1a3a8f);
    }

    .type-row {
      display: grid; grid-template-columns: repeat(3, 1fr);
      gap: 0.8rem; margin: 1.2rem 0;
    }
    .type-card {
      text-align: left; padding: 1rem;
      border-radius: 0.75rem;
      border: 2px solid var(--color-border, #d8dee9);
      background: #fff; cursor: pointer;
      display: grid; gap: 0.3rem;
      font: inherit;
    }
    .type-card.selected {
      border-color: var(--color-primary, #1a3a8f);
      background: #f5f8ff;
    }
    .type-card strong { font-size: 1rem; }
    .type-card span { font-size: 0.82rem; color: var(--color-ink-muted, #4b5a72); }

    .error { color: var(--color-danger, #b02a37); font-weight: 600; }

    @media (max-width: 820px) {
      .type-row { grid-template-columns: 1fr; }
    }
  `,
})
export class OnboardingConsultantComponent {
  protected auth = inject(AuthService);
  private service = inject(OnboardingService);

  protected items = signal<OnboardingItem[]>([]);
  protected loading = signal(false);
  protected error = signal<string | null>(null);
  protected applying = signal(false);
  protected actingId = signal<string | null>(null);
  protected selectedType = signal<ConsultantType>(ConsultantType.Contractor);
  protected notes: Record<string, string> = {};

  protected readonly types = [
    { value: ConsultantType.Contractor, label: 'Contractor (1099)', blurb: 'W-9, services agreement, your own setup.' },
    { value: ConsultantType.FullTime, label: 'Employee (W-2)', blurb: 'I-9, W-4, offer letter, handbook.' },
    { value: ConsultantType.Vendor, label: 'Vendor / agency', blurb: 'Company MSA, insurance, business docs.' },
  ];

  protected total = computed(() => this.items().length);
  protected completed = computed(() => this.items().filter((x) => x.status === 'Approved').length);
  protected percent = computed(() =>
    this.total() === 0 ? 0 : Math.round((this.completed() / this.total()) * 100),
  );

  protected groups = computed<PhaseGroup[]>(() => {
    const all = [...this.items()].sort((a, b) => a.sortOrder - b.sortOrder);
    return PHASE_ORDER
      .map(({ phase, label }) => ({ phase, label, items: all.filter((x) => x.phase === phase) }))
      .filter((g) => g.items.length > 0);
  });

  constructor() {
    this.load();
  }

  protected statusTone(status: string): 'success' | 'info' | 'neutral' | 'warning' {
    if (status === 'Approved') return 'success';
    if (status === 'Submitted') return 'info';
    return 'neutral';
  }
  protected statusLabel(status: string): string {
    if (status === 'Approved') return 'Approved';
    if (status === 'Submitted') return 'In review';
    return 'To do';
  }

  protected phaseTone(g: PhaseGroup): 'success' | 'info' | 'neutral' {
    if (g.items.every((x) => x.status === 'Approved')) return 'success';
    if (g.items.some((x) => x.status === 'Submitted')) return 'info';
    return 'neutral';
  }
  protected phaseLabel(g: PhaseGroup): string {
    if (g.items.every((x) => x.status === 'Approved')) return 'Complete';
    if (g.items.some((x) => x.status === 'Submitted')) return 'In review';
    return 'To do';
  }

  protected setNote(id: string, event: Event): void {
    this.notes[id] = (event.target as HTMLInputElement).value;
  }

  protected begin(): void {
    this.applying.set(true);
    this.error.set(null);
    this.service.applyTemplate(this.selectedType()).subscribe({
      next: (r) => {
        this.applying.set(false);
        this.items.set(r.items);
      },
      error: (e: unknown) => {
        this.applying.set(false);
        this.error.set(this.toMessage(e));
      },
    });
  }

  protected submit(item: OnboardingItem): void {
    this.actingId.set(item.id);
    this.error.set(null);
    this.service.submit(item.id, this.notes[item.id] || null).subscribe({
      next: (updated) => {
        this.actingId.set(null);
        this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      },
      error: (e: unknown) => {
        this.actingId.set(null);
        this.error.set(this.toMessage(e));
      },
    });
  }

  private load(): void {
    if (!this.auth.isAuthenticated()) return;
    this.loading.set(true);
    this.service.mine().subscribe({
      next: (r) => {
        this.loading.set(false);
        this.items.set(r.items);
      },
      error: (e: unknown) => {
        this.loading.set(false);
        if (e instanceof HttpErrorResponse && (e.status === 404 || e.status === 412)) {
          this.items.set([]);
        } else {
          this.error.set(this.toMessage(e));
        }
      },
    });
  }

  private toMessage(e: unknown): string {
    return e instanceof HttpErrorResponse
      ? (e.error?.detail ?? e.error?.title ?? e.message)
      : 'Something went wrong.';
  }
}
