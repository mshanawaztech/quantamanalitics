import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { PublicJobDetail, PublicJobsService } from './core/jobs/public-jobs.service';

@Component({
  selector: 'app-job-detail',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="page">
      <a routerLink="/jobs" class="back-link">Back to jobs</a>

      @if (loading()) {
        <section class="missing-card">
          <h1>Loading job detail…</h1>
          <p>We are pulling the latest published role from the live API.</p>
        </section>
      } @else if (notFound()) {
        <section class="missing-card">
          <h1>Job not found</h1>
          <p>The role you requested is not currently published.</p>
          <a routerLink="/jobs" class="back-link inline-link">Browse active jobs</a>
        </section>
      } @else if (loadError()) {
        <section class="missing-card">
          <h1>We could not load this job right now</h1>
          <p>{{ loadError() }}</p>
          <button type="button" (click)="reload()">Try again</button>
        </section>
      } @else if (job(); as role) {
        <section class="detail-grid">
          <article class="role-card">
            <p class="eyebrow">Open role</p>
            <h1>{{ role.title }}</h1>
            <p class="meta">{{ role.location }} · Posted {{ role.postedOnUtc }}</p>
            <p class="summary">{{ role.summary }}</p>
            <div class="description">{{ role.description }}</div>
          </article>

          <aside class="apply-card">
            <p class="eyebrow">Guest apply</p>
            <h2>Application intake is live.</h2>
            <p>
              Submit interest directly from the public job page. Recruiters can
              now see the intake on their internal board.
            </p>
            <form class="guest-form" (ngSubmit)="apply()">
              <label>
                Full name
                <input type="text" name="fullName" [(ngModel)]="fullName" placeholder="Jane Candidate" required />
              </label>
              <label>
                Email
                <input type="email" name="email" [(ngModel)]="email" placeholder="jane@example.com" required />
              </label>
              <label>
                Short note
                <textarea rows="5" name="note" [(ngModel)]="note" placeholder="Tell us why this role fits."></textarea>
              </label>
              @if (applyError()) {
                <p class="error" role="alert" aria-live="assertive">{{ applyError() }}</p>
              }
              @if (applyMessage()) {
                <p class="success">{{ applyMessage() }}</p>
              }
              <button type="submit" [disabled]="submitting()">
                {{ submitting() ? 'Submitting…' : 'Submit application' }}
              </button>
            </form>
          </aside>
        </section>
      }
    </main>
  `,
  styles: `
    .page { width: min(1120px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .back-link { display: inline-block; margin-bottom: 1rem; color: #9a3412; font-weight: 700; text-decoration: none; }
    .detail-grid { display: grid; grid-template-columns: 1.15fr 0.85fr; gap: 1.25rem; }
    .role-card, .apply-card, .missing-card { padding: 1.6rem; border-radius: 1.5rem; background: rgb(255 251 244 / 0.88); border: 1px solid rgb(87 70 42 / 0.14); box-shadow: 0 1rem 2rem rgb(64 47 22 / 0.06); }
    .eyebrow { margin: 0 0 0.7rem; color: #9a3412; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0; font-size: clamp(2.1rem, 3.6vw, 4rem); line-height: 0.98; }
    h2 { margin-top: 0; font-size: 1.3rem; }
    .meta { color: #8b5e34; }
    .summary, .description, .apply-card p { color: #554d41; line-height: 1.75; }
    .guest-form { display: grid; gap: 0.9rem; }
    label { display: grid; gap: 0.35rem; color: #3f372c; font-weight: 600; }
    input, textarea { width: 100%; padding: 0.85rem 0.95rem; border-radius: 0.9rem; border: 1px solid #d8c8b0; background: #fffdf9; font: inherit; }
    button { padding: 0.9rem 1rem; border: 0; border-radius: 999px; background: #1f2937; color: #fff8ee; font-weight: 700; }
    button[disabled] { opacity: 0.65; cursor: wait; }
    .success { color: #166534; font-weight: 600; }
    .error { color: #b91c1c; font-weight: 600; }
    .inline-link { margin-top: 0.5rem; }
    @media (max-width: 900px) { .detail-grid { grid-template-columns: 1fr; } }
  `,
})
export class JobDetailComponent {
  private route = inject(ActivatedRoute);
  private jobsService = inject(PublicJobsService);

  protected slug = signal('');
  protected job = signal<PublicJobDetail | null>(null);
  protected loading = signal(true);
  protected notFound = signal(false);
  protected loadError = signal<string | null>(null);
  protected submitting = signal(false);
  protected applyMessage = signal<string | null>(null);
  protected applyError = signal<string | null>(null);
  protected fullName = '';
  protected email = '';
  protected note = '';

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => params.get('slug') ?? ''),
        takeUntilDestroyed(),
      )
      .subscribe((slug) => {
        this.slug.set(slug);
        this.loadJob(slug);
      });

    effect(() => {
      const role = this.job();
      if (!role) {
        return;
      }

      this.applyMessage.set(null);
      this.applyError.set(null);
    });
  }

  protected apply(): void {
    const slug = this.slug();
    if (!slug) {
      return;
    }

    this.submitting.set(true);
    this.applyError.set(null);
    this.applyMessage.set(null);

    this.jobsService.apply(slug, {
      fullName: this.fullName,
      email: this.email,
      note: this.note,
    }).subscribe({
      next: (response) => {
        this.submitting.set(false);
        this.applyMessage.set(response.message);
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.applyError.set(
          error instanceof HttpErrorResponse
            ? error.error?.detail ?? error.error?.title ?? error.message
            : error instanceof Error
              ? error.message
            : 'Could not submit application.',
        );
      },
    });
  }

  protected reload(): void {
    this.loadJob(this.slug());
  }

  private loadJob(slug: string): void {
    if (!slug) {
      this.job.set(null);
      this.notFound.set(true);
      this.loading.set(false);
      this.loadError.set(null);
      return;
    }

    this.loading.set(true);
    this.notFound.set(false);
    this.loadError.set(null);

    this.jobsService.detail(slug).subscribe({
      next: (job) => {
        this.job.set(job);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.job.set(null);
        this.loading.set(false);

        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.notFound.set(true);
          return;
        }

        this.loadError.set(
          error instanceof HttpErrorResponse
            ? error.error?.detail ?? error.error?.title ?? error.message
            : error instanceof Error
              ? error.message
              : 'Could not load this job.',
        );
      },
    });
  }
}
