import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import {
  RecruiterApplication,
  RecruiterApplicationsBoard,
  RecruiterJob,
  RecruiterPortalService,
} from './core/recruiter/recruiter-portal.service';

@Component({
  selector: 'app-recruiter-dashboard',
  imports: [FormsModule],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Recruiter portal</p>
          <h1>Jobs and applications on one tenant-safe operating surface.</h1>
          <p>
            This internal workspace gives recruiting users a protected place to
            create jobs and triage candidate applications.
          </p>
        </div>
        <div class="hero-card">
          <p class="label">Access</p>
          <strong>{{ accessLabel() }}</strong>
          <span>{{ roleLabel() }}</span>
        </div>
      </section>

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Recruiter access starts with sign in.</h2>
          <p>Use Auth0 to sign in, then return here to open the recruiter workspace.</p>
          <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
        </section>
      } @else if (!hasRecruitingAccess()) {
        <section class="gate-card">
          <h2>Recruiting access required.</h2>
          <p>Your current session is valid, but this portal is limited to Recruiter or PlatformAdmin roles.</p>
        </section>
      } @else {
        <section class="workspace">
          <article class="jobs-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Jobs workspace</p>
                <h2>Create and review active roles</h2>
              </div>
              @if (loadingJobs()) {
                <span class="pill">Loading</span>
              }
            </div>

            <form class="job-form" (ngSubmit)="createJob()">
              <label>
                Title
                <input type="text" name="title" [(ngModel)]="jobTitle" required />
              </label>
              <label>
                Location
                <input type="text" name="location" [(ngModel)]="jobLocation" required />
              </label>
              <label class="full-width">
                Summary
                <input type="text" name="summary" [(ngModel)]="jobSummary" required />
              </label>
              <label class="full-width">
                Description
                <textarea rows="5" name="description" [(ngModel)]="jobDescription" required></textarea>
              </label>
              <label class="checkbox full-width">
                <input type="checkbox" name="published" [(ngModel)]="jobPublished" />
                Publish immediately
              </label>
              <div class="actions full-width">
                <button type="submit" class="primary" [disabled]="creatingJob()">
                  {{ creatingJob() ? 'Creating…' : 'Create job' }}
                </button>
              </div>
            </form>

            @if (jobsError()) {
              <p class="error">{{ jobsError() }}</p>
            }

            <div class="jobs-list">
              @for (job of jobs(); track job.id) {
                <article class="job-item">
                  <div>
                    <strong>{{ job.title }}</strong>
                    <p>{{ job.location }} · {{ job.isPublished ? 'Published' : 'Draft' }}</p>
                  </div>
                  <span class="slug">{{ job.slug }}</span>
                </article>
              } @empty {
                <p class="empty">No tenant jobs yet.</p>
              }
            </div>
          </article>

          <article class="board-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Applications board</p>
                <h2>Move candidate intake across the funnel</h2>
              </div>
              @if (loadingApplications()) {
                <span class="pill">Loading</span>
              }
            </div>

            @if (applicationsError()) {
              <p class="error">{{ applicationsError() }}</p>
            }

            <div class="board">
              @for (column of columns; track column) {
                <section class="column">
                  <h3>{{ column }}</h3>
                  @for (application of applicationsByStatus(column); track application.id) {
                    <article class="application-card">
                      <strong>{{ application.candidateName }}</strong>
                      <p>{{ application.jobTitle }}</p>
                      <p>{{ application.candidateEmail }}</p>
                      @if (application.note) {
                        <p>{{ application.note }}</p>
                      }
                      <label>
                        Move to
                        <select [ngModel]="application.status" (ngModelChange)="move(application, $event)">
                          @for (status of columns; track status) {
                            <option [ngValue]="status">{{ status }}</option>
                          }
                        </select>
                      </label>
                    </article>
                  } @empty {
                    <p class="empty">No applications in this stage.</p>
                  }
                </section>
              }
            </div>
          </article>
        </section>
      }
    </main>
  `,
  styles: `
    .page { width: min(1180px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero, .workspace { display: grid; gap: 1.25rem; }
    .hero { grid-template-columns: 1.15fr 0.85fr; margin-bottom: 1.5rem; }
    .workspace { grid-template-columns: 0.95fr 1.05fr; align-items: start; }
    .hero-card, .gate-card, .jobs-card, .board-card, .column, .application-card {
      border-radius: 1.5rem;
      background: rgb(255 251 244 / 0.88);
      border: 1px solid rgb(87 70 42 / 0.14);
      box-shadow: 0 1rem 2rem rgb(64 47 22 / 0.06);
    }
    .hero-card, .gate-card, .jobs-card, .board-card { padding: 1.6rem; }
    .eyebrow { margin: 0 0 0.7rem; color: #9a3412; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.8rem; font-size: clamp(2.1rem, 3.8vw, 4.2rem); line-height: 0.98; }
    h2, h3 { margin: 0; }
    p { color: #554d41; line-height: 1.65; }
    .label { margin: 0 0 0.4rem; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; color: #9a3412; font-weight: 800; }
    .hero-card strong { display: block; font-size: 1.2rem; margin-bottom: 0.45rem; }
    .section-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1rem; }
    .pill { padding: 0.35rem 0.7rem; border-radius: 999px; background: #e7e5e4; color: #44403c; font-size: 0.85rem; font-weight: 700; }
    .job-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.95rem; margin-bottom: 1.25rem; }
    label { display: grid; gap: 0.35rem; color: #3f372c; font-weight: 600; }
    input, textarea, select {
      width: 100%;
      padding: 0.85rem 0.95rem;
      border-radius: 0.9rem;
      border: 1px solid #d8c8b0;
      background: #fffdf9;
      font: inherit;
    }
    .checkbox { display: flex; align-items: center; gap: 0.6rem; }
    .checkbox input { width: auto; }
    .full-width { grid-column: 1 / -1; }
    .actions { display: flex; gap: 0.8rem; flex-wrap: wrap; }
    .primary {
      padding: 0.9rem 1rem;
      border: 0;
      border-radius: 999px;
      background: #1f2937;
      color: #fff8ee;
      font-weight: 700;
      cursor: pointer;
    }
    .jobs-list { display: grid; gap: 0.8rem; }
    .job-item, .application-card { padding: 1rem; }
    .job-item { display: flex; justify-content: space-between; gap: 1rem; border-radius: 1.1rem; background: #fffdf9; border: 1px solid #eadcc8; }
    .slug { font-family: monospace; color: #8b5e34; }
    .board { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1rem; }
    .column { padding: 1rem; display: grid; gap: 0.8rem; align-content: start; min-height: 20rem; }
    .application-card { background: #fffdf9; border: 1px solid #eadcc8; }
    .empty { color: #7c6f5e; }
    .error { color: #b91c1c; font-weight: 600; }
    @media (max-width: 980px) {
      .hero, .workspace, .board, .job-form { grid-template-columns: 1fr; }
    }
  `,
})
export class RecruiterDashboardComponent {
  protected auth = inject(AuthService);
  protected me = inject(MeService);
  private recruiter = inject(RecruiterPortalService);

  protected readonly columns = ['Applied', 'Interviewing', 'OfferSent', 'Hired', 'Rejected'];

  protected jobs = signal<RecruiterJob[]>([]);
  protected applications = signal<RecruiterApplication[]>([]);
  protected loadingJobs = signal(false);
  protected loadingApplications = signal(false);
  protected creatingJob = signal(false);
  protected jobsError = signal<string | null>(null);
  protected applicationsError = signal<string | null>(null);

  protected jobTitle = '';
  protected jobLocation = '';
  protected jobSummary = '';
  protected jobDescription = '';
  protected jobPublished = true;

  constructor() {
    effect(() => {
      if (!this.hasRecruitingAccess()) {
        return;
      }

      this.fetchJobs();
      this.fetchApplications();
    });
  }

  protected hasRecruitingAccess(): boolean {
    const roles = this.me.data()?.roles ?? [];
    return roles.includes('Recruiter') || roles.includes('PlatformAdmin');
  }

  protected accessLabel(): string {
    if (!this.auth.isAuthenticated()) {
      return 'Awaiting sign in';
    }

    return this.hasRecruitingAccess() ? 'Recruiter workspace ready' : 'Authenticated without recruiter access';
  }

  protected roleLabel(): string {
    return this.me.data()?.roles.join(', ') || 'Role not yet available';
  }

  protected applicationsByStatus(status: string): RecruiterApplication[] {
    return this.applications().filter((application) => application.status === status);
  }

  protected createJob(): void {
    this.creatingJob.set(true);
    this.jobsError.set(null);

    this.recruiter.createJob({
      title: this.jobTitle,
      location: this.jobLocation,
      summary: this.jobSummary,
      description: this.jobDescription,
      isPublished: this.jobPublished,
      postedOnUtc: null,
    }).subscribe({
      next: (job) => {
        this.jobs.update((current) => [job, ...current]);
        this.creatingJob.set(false);
        this.jobTitle = '';
        this.jobLocation = '';
        this.jobSummary = '';
        this.jobDescription = '';
        this.jobPublished = true;
      },
      error: (error: unknown) => {
        this.creatingJob.set(false);
        this.jobsError.set(this.toErrorMessage(error));
      },
    });
  }

  protected move(application: RecruiterApplication, status: string): void {
    if (application.status === status) {
      return;
    }

    this.recruiter.moveApplication(application.id, status).subscribe({
      next: (updated) => {
        this.applications.update((items) =>
          items.map((item) => item.id === updated.id ? updated : item),
        );
      },
      error: (error: unknown) => {
        this.applicationsError.set(this.toErrorMessage(error));
      },
    });
  }

  private fetchJobs(): void {
    this.loadingJobs.set(true);
    this.recruiter.jobs().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        this.loadingJobs.set(false);
      },
      error: (error: unknown) => {
        this.loadingJobs.set(false);
        this.jobsError.set(this.toErrorMessage(error));
      },
    });
  }

  private fetchApplications(): void {
    this.loadingApplications.set(true);
    this.recruiter.applications().subscribe({
      next: (board: RecruiterApplicationsBoard) => {
        this.applications.set(board.items);
        this.loadingApplications.set(false);
      },
      error: (error: unknown) => {
        this.loadingApplications.set(false);
        this.applicationsError.set(this.toErrorMessage(error));
      },
    });
  }

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ?? error.error?.title ?? error.message;
    }

    return error instanceof Error ? error.message : 'Unexpected recruiter portal error.';
  }
}
