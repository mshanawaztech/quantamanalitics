import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';

type ScorecardTemplate = {
  title: string;
  audience: string;
  sections: string[];
};

type SchedulerProvider = {
  name: string;
  status: string;
  detail: string;
};

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
            This shell gives Phase 3 a real home before provider integrations land.
            Recruiters can see the scorecard kits, scheduler readiness, and the
            handoff states that will connect submissions to calendar events.
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
            <dd>Interview shell + scorecards</dd>
            <dt>Next slice</dt>
            <dd>Google / Outlook provider baseline</dd>
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
          <h2>Recruiting access required.</h2>
          <p>
            Your session is valid, but only Recruiter or PlatformAdmin roles can
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
                <h2>Provider surfaces that Phase 3 will wire next</h2>
              </div>
              <span class="pill">{{ activeProviderCount() }} active next-step integrations</span>
            </div>

            <div class="provider-list">
              @for (provider of providers; track provider.name) {
                <article class="provider-item">
                  <div class="provider-head">
                    <strong>{{ provider.name }}</strong>
                    <span class="provider-status">{{ provider.status }}</span>
                  </div>
                  <p>{{ provider.detail }}</p>
                </article>
              }
            </div>
          </article>
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
              <p>Once the client accepts the handoff, this shell becomes the home for interview creation.</p>
            </article>
            <article class="flow-item">
              <strong>Calendar booked</strong>
              <p>Google and Outlook connectors will attach event details and availability handling in the next PR.</p>
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
    .hero-card, .gate-card, .scorecard-card, .scheduler-card, .flow-card, .scorecard-item, .provider-item, .flow-item {
      padding: 1.6rem;
      border-radius: 1.5rem;
      background: rgb(255 251 244 / 0.88);
      border: 1px solid rgb(87 70 42 / 0.14);
      box-shadow: 0 1rem 2rem rgb(64 47 22 / 0.06);
    }
    .eyebrow { margin: 0 0 0.7rem; color: #9a3412; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.8rem; font-size: clamp(2.1rem, 3.8vw, 4.2rem); line-height: 0.98; }
    h2 { margin: 0; font-size: 1.4rem; }
    p, li { color: #554d41; line-height: 1.7; }
    .label { margin: 0 0 0.4rem; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; color: #9a3412; font-weight: 800; }
    .hero-card strong { display: block; font-size: 1.2rem; margin-bottom: 0.45rem; }
    .hero-card span { color: #6b6255; }
    .hero-meta {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 0.45rem 0.9rem;
      margin: 1rem 0 0;
    }
    .hero-meta dt { font-weight: 700; color: #3f372c; }
    .hero-meta dd { margin: 0; color: #554d41; word-break: break-word; }
    .section-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1rem; }
    .pill {
      padding: 0.35rem 0.7rem;
      border-radius: 999px;
      background: #e7e5e4;
      color: #44403c;
      font-size: 0.85rem;
      font-weight: 700;
    }
    .actions { display: flex; gap: 0.8rem; align-items: center; flex-wrap: wrap; }
    .primary {
      padding: 0.9rem 1rem;
      border: 0;
      border-radius: 999px;
      background: #1f2937;
      color: #fff8ee;
      font-weight: 700;
      cursor: pointer;
    }
    a { color: #9a3412; font-weight: 700; text-decoration: none; }
    .scorecard-list, .provider-list, .flow-grid {
      display: grid;
      gap: 0.9rem;
      grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
    }
    .scorecard-item, .provider-item, .flow-item { background: #fffdf9; }
    .scorecard-item strong, .provider-item strong, .flow-item strong { display: block; margin-bottom: 0.45rem; }
    ul { margin: 0.75rem 0 0; padding-left: 1.1rem; }
    .provider-head {
      display: flex;
      justify-content: space-between;
      gap: 0.8rem;
      align-items: baseline;
      margin-bottom: 0.5rem;
    }
    .provider-status { color: #9a3412; font-weight: 700; }
    @media (max-width: 980px) {
      .hero, .workspace { grid-template-columns: 1fr; }
    }
  `,
})
export class InterviewSchedulingComponent {
  protected auth = inject(AuthService);
  protected me = inject(MeService);

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

  protected readonly providers: SchedulerProvider[] = [
    {
      name: 'Google Calendar baseline',
      status: 'Next PR',
      detail: 'Provider abstraction and event persistence will start here for recruiter-led scheduling.',
    },
    {
      name: 'Outlook baseline',
      status: 'Next PR',
      detail: 'Same interview surface, alternate calendar provider path for Microsoft-centered teams.',
    },
    {
      name: 'Meeting link scaffold',
      status: 'Queued',
      detail: 'Zoom / Teams link generation lands after provider persistence is in place.',
    },
  ];

  protected readonly activeProviderCount = computed(
    () => this.providers.filter((provider) => provider.status !== 'Queued').length,
  );

  protected hasRecruitingAccess(): boolean {
    const roles = this.me.data()?.roles ?? [];
    return roles.includes('Recruiter') || roles.includes('PlatformAdmin');
  }

  protected accessLabel(): string {
    if (!this.auth.isAuthenticated()) {
      return 'Awaiting sign in';
    }

    return this.hasRecruitingAccess() ? 'Interview workflow shell ready' : 'Authenticated without recruiter access';
  }

  protected roleLabel(): string {
    return this.me.data()?.roles.join(', ') || 'Role not yet available';
  }
}
