import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import {
  InterviewOverviewEvent,
  InterviewOverviewProvider,
  InterviewSchedulingService,
} from './core/interviews/interview-scheduling.service';

interface ScorecardTemplate {
  title: string;
  audience: string;
  sections: string[];
}

@Component({
  selector: 'app-interview-scheduling',
  imports: [RouterLink],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Interview scheduling</p>
          <h1>Turn approved submissions into a repeatable interview cadence.</h1>
          <p>
            This route now shows live provider readiness and seeded interview
            events so the Phase 3 calendar work has a persistent place to grow.
          </p>
        </div>
        <div class="hero-card">
          <p class="label">Workspace access</p>
          <strong>{{ accessLabel() }}</strong>
          <span>{{ roleLabel() }}</span>
          <dl class="hero-meta">
            <dt>Tenant</dt>
            <dd>{{ me.data()?.tenantId || 'Pending tenant claim' }}</dd>
            <dt>Current slice</dt>
            <dd>Google / Outlook baseline</dd>
            <dt>Next slice</dt>
            <dd>Meeting link scaffold</dd>
          </dl>
        </div>
      </section>

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Interview workflow starts with sign in.</h2>
          <p>
            Use Auth0 to enter the protected interview planning area, then return
            here to continue from submissions into calendar-linked steps.
          </p>
          <div class="actions">
            <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
            <a routerLink="/recruiter">Go back to recruiter workspace</a>
          </div>
        </section>
      } @else if (!hasRecruitingAccess()) {
        <section class="gate-card">
          <h2>Interview access required.</h2>
          <p>
            Your session is valid, but only interview-enabled roles such as
            Interviewer, Recruiter, HR admin, manager, or PlatformAdmin can
            open interview planning surfaces.
          </p>
        </section>
      } @else {
        <section class="workspace">
          <article class="scorecard-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Scorecard kits</p>
                <h2>Starter rubrics for repeatable interview loops</h2>
              </div>
              <span class="pill">{{ scorecards.length }} templates</span>
            </div>

            <div class="scorecard-list">
              @for (scorecard of scorecards; track scorecard.title) {
                <article class="scorecard-item">
                  <strong>{{ scorecard.title }}</strong>
                  <p>{{ scorecard.audience }}</p>
                  <ul>
                    @for (section of scorecard.sections; track section) {
                      <li>{{ section }}</li>
                    }
                  </ul>
                </article>
              }
            </div>
          </article>

          <article class="scheduler-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Scheduler readiness</p>
                <h2>Provider baselines now backed by live API state</h2>
              </div>
              @if (loadingOverview()) {
                <span class="pill">Loading</span>
              } @else {
                <span class="pill">{{ activeProviderCount() }} active provider paths</span>
              }
            </div>

            @if (overviewError()) {
              <p class="error" role="alert" aria-live="assertive">{{ overviewError() }}</p>
            }

            <div class="provider-list">
              @for (provider of providers(); track provider.name) {
                <article class="provider-item">
                  <div class="provider-head">
                    <strong>{{ provider.name }}</strong>
                    <span class="provider-status">{{ provider.status }}</span>
                  </div>
                  <p>{{ provider.detail }}</p>
                </article>
              } @empty {
                <p class="empty">No provider baselines are registered for this environment yet.</p>
              }
            </div>
          </article>
        </section>

        <section class="events-card">
          <div class="section-head">
            <div>
              <p class="eyebrow">Interview events</p>
              <h2>Submission-linked sessions ready for calendar expansion</h2>
            </div>
            <span class="pill">{{ events().length }} scheduled events</span>
          </div>

          <div class="events-grid">
            @for (event of events(); track event.id) {
              <article class="event-item">
                <strong>{{ event.title }}</strong>
                <p>{{ event.candidateName }} · {{ event.candidateEmail }}</p>
                <p>{{ event.interviewerName }} · {{ providerLabel(event.provider) }}</p>
                <p>{{ formatWindow(event) }}</p>
                <p>Status: {{ event.status }}</p>
              </article>
            } @empty {
              <p class="empty">No interview events are scheduled for this tenant yet.</p>
            }
          </div>
        </section>

        <section class="flow-card">
          <div class="section-head">
            <div>
              <p class="eyebrow">Workflow map</p>
              <h2>Submission-to-interview state story</h2>
            </div>
          </div>

          <div class="flow-grid">
            <article class="flow-item">
              <strong>Submission drafted</strong>
              <p>Recruiter prepares a client-ready candidate pitch from the new submission aggregate.</p>
            </article>
            <article class="flow-item">
              <strong>Client reviewing</strong>
              <p>Once the client accepts the handoff, this route becomes the home for persistent interview creation.</p>
            </article>
            <article class="flow-item">
              <strong>Calendar booked</strong>
              <p>Google and Outlook provider records now exist; meeting-link generation lands in the next PR.</p>
            </article>
            <article class="flow-item">
              <strong>Scorecard captured</strong>
              <p>Interview feedback will land against these reusable scorecard kits before offer/onboarding work begins.</p>
            </article>
          </div>
        </section>
      }
    </main>
  `,
  styles: `
    .page { width: min(1180px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero, .workspace { display: grid; gap: 1.25rem; }
    .hero { grid-template-columns: 1.1fr 0.9fr; margin-bottom: 1.5rem; }
    .workspace { grid-template-columns: minmax(0, 1.05fr) minmax(0, 0.95fr); align-items: start; margin-bottom: 1.25rem; }
    .hero-card, .gate-card, .scorecard-card, .scheduler-card, .flow-card, .events-card, .scorecard-item, .provider-item, .flow-item, .event-item {
      padding: 1.6rem;
      border-radius: 1.5rem;
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
      box-shadow: var(--shadow-md);
    }
    .eyebrow { margin: 0 0 0.7rem; color: var(--color-primary, #1a3a8f); text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.8rem; font-size: clamp(2.1rem, 3.8vw, 4.2rem); line-height: 0.98; }
    h2 { margin: 0; font-size: 1.4rem; }
    p, li { color: var(--color-ink-muted, #4b5a72); line-height: 1.7; }
    .label { margin: 0 0 0.4rem; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; color: var(--color-primary, #1a3a8f); font-weight: 800; }
    .hero-card strong { display: block; font-size: 1.2rem; margin-bottom: 0.45rem; }
    .hero-card span { color: var(--color-ink-muted, #4b5a72); }
    .hero-meta {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 0.45rem 0.9rem;
      margin: 1rem 0 0;
    }
    .hero-meta dt { font-weight: 700; color: var(--color-ink, #1a2942); }
    .hero-meta dd { margin: 0; color: var(--color-ink-muted, #4b5a72); word-break: break-word; }
    .section-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1rem; }
    .pill {
      padding: 0.35rem 0.7rem;
      border-radius: 999px;
      background: var(--color-surface-alt, #f0f3f9);
      color: var(--color-ink, #1a2942);
      font-size: 0.85rem;
      font-weight: 700;
    }
    .actions { display: flex; gap: 0.8rem; align-items: center; flex-wrap: wrap; }
    .primary {
      padding: 0.9rem 1rem;
      border: 0;
      border-radius: 999px;
      background: var(--color-primary, #1a3a8f);
      color: var(--color-ink-onblue, #ffffff);
      font-weight: 700;
      cursor: pointer;
    }
    a { color: var(--color-primary, #1a3a8f); font-weight: 700; text-decoration: none; }
    .scorecard-list, .provider-list, .flow-grid, .events-grid {
      display: grid;
      gap: 0.9rem;
      grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
    }
    .events-card { margin-bottom: 1.25rem; }
    .scorecard-item, .provider-item, .flow-item, .event-item { background: var(--color-surface, #ffffff); }
    .scorecard-item strong, .provider-item strong, .flow-item strong, .event-item strong { display: block; margin-bottom: 0.45rem; }
    ul { margin: 0.75rem 0 0; padding-left: 1.1rem; }
    .provider-head {
      display: flex;
      justify-content: space-between;
      gap: 0.8rem;
      align-items: baseline;
      margin-bottom: 0.5rem;
    }
    .provider-status { color: var(--color-primary, #1a3a8f); font-weight: 700; }
    .event-item p { margin: 0.2rem 0; }
    .error { color: #b91c1c; font-weight: 600; }
    .empty { color: var(--color-ink-muted, #4b5a72); }
    @media (max-width: 980px) {
      .hero, .workspace { grid-template-columns: 1fr; }
    }
  `,
})
export class InterviewSchedulingComponent {
  protected auth = inject(AuthService);
  protected me = inject(MeService);
  protected access = inject(AccessService);
  private interviews = inject(InterviewSchedulingService);

  protected readonly scorecards: ScorecardTemplate[] = [
    {
      title: 'Recruiter screen',
      audience: 'Internal recruiter calibration before the client sees the candidate',
      sections: ['Role fit and domain depth', 'Communication signal', 'Comp / location alignment'],
    },
    {
      title: 'Hiring manager sync',
      audience: 'Client-facing interview loop for technical or delivery stakeholders',
      sections: ['Hands-on capability', 'Stakeholder confidence', 'Project readiness'],
    },
    {
      title: 'Closeout review',
      audience: 'Offer readiness and onboarding handoff checkpoint',
      sections: ['Decision summary', 'Risks / blockers', 'Start-date confidence'],
    },
  ];

  protected providers = signal<InterviewOverviewProvider[]>([]);
  protected events = signal<InterviewOverviewEvent[]>([]);
  protected loadingOverview = signal(false);
  protected overviewError = signal<string | null>(null);

  protected readonly activeProviderCount = computed(
    () => this.providers().filter((provider) => provider.status !== 'Queued').length,
  );

  constructor() {
    effect(() => {
      if (!this.auth.isAuthenticated() || !this.hasRecruitingAccess()) {
        this.providers.set([]);
        this.events.set([]);
        this.loadingOverview.set(false);
        this.overviewError.set(null);
        return;
      }

      this.loadOverview();
    });
  }

  protected hasRecruitingAccess(): boolean {
    return this.access.canAccessInterviewWorkspace();
  }

  protected accessLabel(): string {
    if (!this.auth.isAuthenticated()) {
      return 'Awaiting sign in';
    }

    return this.hasRecruitingAccess()
      ? 'Interview workspace ready'
      : 'Authenticated without interview access';
  }

  protected roleLabel(): string {
    return this.me.data()?.roles.join(', ') || 'Role not yet available';
  }

  protected providerLabel(provider: string): string {
    return provider === 'GoogleCalendar' ? 'Google Calendar'
      : provider === 'OutlookCalendar' ? 'Outlook Calendar'
      : provider;
  }

  protected formatWindow(event: InterviewOverviewEvent): string {
    const start = new Date(event.scheduledStartUtc);
    const end = new Date(event.scheduledEndUtc);
    return `${start.toLocaleString()} - ${end.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}`;
  }

  private loadOverview(): void {
    this.loadingOverview.set(true);
    this.overviewError.set(null);

    this.interviews.overview().subscribe({
      next: (payload) => {
        this.providers.set(payload.providers);
        this.events.set(payload.events);
        this.loadingOverview.set(false);
      },
      error: (error: unknown) => {
        const detail = error instanceof HttpErrorResponse
          ? error.error?.detail || error.error?.title || error.message
          : 'Unable to load interview overview.';

        this.overviewError.set(detail);
        this.providers.set([]);
        this.events.set([]);
        this.loadingOverview.set(false);
      },
    });
  }
}
