import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map, switchMap } from 'rxjs';
import { PublicJobsService } from './core/jobs/public-jobs.service';

@Component({
  selector: 'app-job-detail',
  imports: [RouterLink],
  template: `
    <main class="page">
      <a routerLink="/jobs" class="back-link">Back to jobs</a>

      @if (job(); as role) {
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
            <h2>Application intake is the next step.</h2>
            <p>
              This scaffold keeps the public job detail live and ready while the
              candidate application flow lands in the next feature slice.
            </p>
            <form class="guest-form">
              <label>
                Full name
                <input type="text" placeholder="Jane Candidate" />
              </label>
              <label>
                Email
                <input type="email" placeholder="jane@example.com" />
              </label>
              <label>
                Short note
                <textarea rows="5" placeholder="Tell us why this role fits."></textarea>
              </label>
              <button type="button" disabled>Apply flow coming next</button>
            </form>
          </aside>
        </section>
      } @else {
        <section class="missing-card">
          <h1>Job not found</h1>
          <p>The role you requested is not currently published.</p>
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
    button { padding: 0.9rem 1rem; border: 0; border-radius: 999px; background: #d6d3d1; color: #57534e; font-weight: 700; }
    @media (max-width: 900px) { .detail-grid { grid-template-columns: 1fr; } }
  `,
})
export class JobDetailComponent {
  private route = inject(ActivatedRoute);
  private jobsService = inject(PublicJobsService);

  protected slug = toSignal(
    this.route.paramMap.pipe(map((params) => params.get('slug') ?? '')),
    { initialValue: '' },
  );

  protected job = toSignal(
    this.route.paramMap.pipe(
      map((params) => params.get('slug') ?? ''),
      switchMap((slug) => this.jobsService.detail(slug)),
    ),
    { initialValue: null },
  );
}
