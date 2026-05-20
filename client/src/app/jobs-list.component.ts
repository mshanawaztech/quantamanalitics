import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PublicJobListItem, PublicJobsService } from './core/jobs/public-jobs.service';

@Component({
  selector: 'app-jobs-list',
  imports: [RouterLink],
  template: `
    <main class="page">
      <section class="hero">
        <p class="eyebrow">Public job board</p>
        <h1>Open roles published from the Quantam Analytics staffing stack.</h1>
        <p>
          This is the first anonymous candidate-facing workflow in the product.
          Browse jobs, inspect detail, and move toward an application flow.
        </p>
      </section>

      <section class="jobs-grid">
        @if (loading()) {
          <article class="job-card state-card">
            <h2>Loading live roles…</h2>
            <p>We are checking the public job board now.</p>
          </article>
        } @else if (error()) {
          <article class="job-card state-card empty">
            <h2>Could not load jobs right now</h2>
            <p>{{ error() }}</p>
            <button
              type="button"
              class="retry"
              (click)="loadJobs()"
              aria-label="Retry loading jobs"
            >Try again</button>
          </article>
        } @else {
          @for (job of jobs(); track job.id) {
            <article class="job-card">
              <p class="meta">{{ job.location }} · Posted {{ job.postedOnUtc }}</p>
              <h2>{{ job.title }}</h2>
              <p>{{ job.summary }}</p>
              <a [routerLink]="['/jobs', job.slug]">View role</a>
            </article>
          } @empty {
            <article class="job-card empty">
              <h2>No jobs published yet</h2>
              <p>The public board is online, but there are no published roles to show.</p>
            </article>
          }
        }
      </section>
    </main>
  `,
  styles: `
    .page { width: min(1120px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero { max-width: 52rem; margin-bottom: 2rem; }
    .eyebrow { margin: 0 0 0.7rem; color: var(--color-primary, #1a3a8f); text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0; font-size: clamp(2.2rem, 3.8vw, 4.4rem); line-height: 0.98; }
    .hero p:last-child { color: var(--color-ink-muted, #4b5a72); line-height: 1.75; font-size: 1.08rem; }
    .jobs-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1.25rem; }
    .job-card { padding: 1.5rem; border-radius: 1.5rem; background: var(--color-surface, #ffffff); border: 1px solid var(--color-border, #d8dee9); box-shadow: var(--shadow-md); }
    .meta { margin: 0 0 0.8rem; color: var(--color-ink-muted, #4b5a72); font-size: 0.92rem; }
    h2 { margin: 0 0 0.75rem; font-size: 1.35rem; }
    p { color: var(--color-ink-muted, #4b5a72); line-height: 1.7; }
    a { color: var(--color-primary, #1a3a8f); font-weight: 700; text-decoration: none; }
    .state-card { display: grid; gap: 0.8rem; }
    .retry {
      justify-self: start;
      padding: 0.85rem 1rem;
      border: 0;
      border-radius: 999px;
      background: var(--color-primary, #1a3a8f);
      color: var(--color-ink-onblue, #ffffff);
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }
    .empty { grid-column: 1 / -1; }
    @media (max-width: 860px) { .jobs-grid { grid-template-columns: 1fr; } }
  `,
})
export class JobsListComponent {
  private jobsService = inject(PublicJobsService);

  protected jobs = signal<PublicJobListItem[]>([]);
  protected loading = signal(true);
  protected error = signal<string | null>(null);

  constructor() {
    this.loadJobs();
  }

  protected loadJobs(): void {
    this.loading.set(true);
    this.error.set(null);

    this.jobsService.list().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.jobs.set([]);
        this.loading.set(false);
        this.error.set(this.toErrorMessage(error));
      },
    });
  }

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ?? error.error?.title ?? error.message;
    }

    return error instanceof Error ? error.message : 'Unexpected error loading jobs.';
  }
}
