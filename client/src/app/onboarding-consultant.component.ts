import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { AuthService } from './core/auth/auth.service';
import {
  ConsultantType,
  OnboardingItem,
  OnboardingPhase,
  OnboardingService,
} from './core/onboarding/onboarding.service';

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
  imports: [],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Your onboarding</p>
          <h1>Let's get you ready to work.</h1>
          <p>Complete each step below. We'll review what you submit and let you know if anything needs a fix.</p>
        </div>
        <div class="hero-card">
          <p class="label">Progress</p>
          <strong>{{ percent() }}%</strong>
          <span>{{ completed() }} of {{ total() }} steps done</span>
        </div>
      </section>

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Sign in to start onboarding.</h2>
          <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
        </section>
      } @else if (error()) {
        <section class="gate-card"><p class="error">{{ error() }}</p></section>
      } @else if (total() === 0 && !loading()) {
        <section class="gate-card">
          <h2>Start your onboarding</h2>
          <p>Pick what best describes your engagement and we'll build your checklist.</p>
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
          <button type="button" class="primary" [disabled]="applying()" (click)="begin()">
            {{ applying() ? 'Setting up…' : 'Begin onboarding' }}
          </button>
        </section>
      } @else {
        <div class="bar"><div class="bar-fill" [style.width.%]="percent()"></div></div>

        @for (group of groups(); track group.phase) {
          <section class="phase-card">
            <h2>{{ group.label }}</h2>
            <div class="steps">
              @for (item of group.items; track item.id) {
                <article class="step" [class.done]="item.status === 'Approved'">
                  <div class="step-main">
                    <div class="step-text">
                      <strong>
                        {{ item.title }}
                        @if (!item.isRequired) { <span class="optional">Optional</span> }
                      </strong>
                      @if (item.instructions) { <p>{{ item.instructions }}</p> }
                      @if (item.status === 'Approved' && item.reviewerNote) {
                        <p class="note">Reviewer: {{ item.reviewerNote }}</p>
                      }
                    </div>
                    <span class="status" [class]="statusClass(item.status)">{{ statusLabel(item.status) }}</span>
                  </div>

                  @if (item.status === 'Pending') {
                    <div class="step-action">
                      <input
                        type="text"
                        [value]="notes[item.id] || ''"
                        (input)="setNote(item.id, $event)"
                        placeholder="Add a note (optional)"
                      />
                      <button type="button" class="primary" [disabled]="actingId() === item.id" (click)="submit(item)">
                        {{ actingId() === item.id ? 'Submitting…' : 'Mark done' }}
                      </button>
                    </div>
                  }
                </article>
              }
            </div>
          </section>
        }
      }
    </main>
  `,
  styles: `
    .page { width: min(900px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero { display: grid; grid-template-columns: 1.15fr 0.85fr; gap: 1.25rem; margin-bottom: 1.5rem; }
    .hero-card, .gate-card, .phase-card, .step {
      padding: 1.5rem; border-radius: 1.25rem;
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
      box-shadow: var(--shadow-md);
    }
    .eyebrow { margin: 0 0 0.6rem; color: var(--color-primary, #1a3a8f); text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.6rem; font-size: clamp(1.9rem, 3.4vw, 3.2rem); line-height: 1.02; }
    h2 { margin: 0 0 1rem; font-size: 1.2rem; }
    p { color: var(--color-ink-muted, #4b5a72); line-height: 1.6; margin: 0; }
    .label { margin: 0 0 0.35rem; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; color: var(--color-primary, #1a3a8f); font-weight: 800; }
    .hero-card strong { display: block; font-size: 2.4rem; line-height: 1; margin-bottom: 0.3rem; color: var(--color-primary, #1a3a8f); }
    .bar { height: 10px; border-radius: 999px; background: var(--color-surface-alt, #eef2f9); overflow: hidden; margin-bottom: 1.5rem; }
    .bar-fill { height: 100%; border-radius: 999px; background: linear-gradient(90deg, #1e5fd0, #1733a6); transition: width 0.3s ease; }
    .phase-card { margin-bottom: 1.25rem; }
    .steps { display: grid; gap: 0.8rem; }
    .step { padding: 1.1rem 1.25rem; }
    .step.done { background: #f5fbf7; }
    .step-main { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; }
    .step-text strong { display: block; margin-bottom: 0.25rem; }
    .step-text p { font-size: 0.92rem; }
    .note { font-style: italic; }
    .optional { font-size: 0.7rem; font-weight: 700; color: #64748b; background: var(--color-surface-alt, #eef2f9); padding: 0.1rem 0.45rem; border-radius: 999px; margin-left: 0.4rem; vertical-align: middle; }
    .status { padding: 0.3rem 0.7rem; border-radius: 999px; font-size: 0.78rem; font-weight: 700; white-space: nowrap; }
    .status--pending { background: var(--color-surface-alt, #f0f3f9); color: #475569; }
    .status--review { background: #dbeafe; color: #1e40af; }
    .status--approved { background: #dcfce7; color: #166534; }
    .step-action { display: flex; gap: 0.7rem; margin-top: 0.9rem; }
    .step-action input { flex: 1; padding: 0.65rem 0.8rem; border-radius: 0.7rem; border: 1px solid var(--color-border, #d8dee9); font: inherit; }
    .type-row { display: grid; grid-template-columns: repeat(3, 1fr); gap: 0.8rem; margin: 1.2rem 0; }
    .type-card { text-align: left; padding: 1rem; border-radius: 0.9rem; border: 2px solid var(--color-border, #d8dee9); background: var(--color-surface, #fff); cursor: pointer; display: grid; gap: 0.3rem; }
    .type-card.selected { border-color: var(--color-primary, #1a3a8f); background: #f5f8ff; }
    .type-card strong { font-size: 1rem; }
    .type-card span { font-size: 0.82rem; color: var(--color-ink-muted, #4b5a72); }
    .primary { padding: 0.85rem 1.1rem; border-radius: 999px; border: 0; background: var(--color-primary, #1a3a8f); color: #fff; font-weight: 700; cursor: pointer; }
    .primary:disabled { opacity: 0.6; cursor: default; }
    .error { color: #b91c1c; font-weight: 600; }
    @media (max-width: 820px) { .hero, .type-row { grid-template-columns: 1fr; } }
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

  protected statusClass(status: string): string {
    if (status === 'Approved') return 'status--approved';
    if (status === 'Submitted') return 'status--review';
    return 'status--pending';
  }

  protected statusLabel(status: string): string {
    if (status === 'Approved') return 'Approved';
    if (status === 'Submitted') return 'In review';
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
        // A 404/empty just means "no checklist yet" — show the start screen.
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
