import { Component } from '@angular/core';

@Component({
  selector: 'app-services',
  template: `
    <main class="page">
      <section class="hero">
        <p class="eyebrow">Services</p>
        <h1>A platform shape tuned for recruiting, delivery, and client visibility.</h1>
      </section>

      <section class="services-grid">
        <article>
          <span>01</span>
          <h2>Candidate pipeline orchestration</h2>
          <p>Track intake, screening, submission readiness, and downstream decisions in one recruiter-friendly flow.</p>
        </article>
        <article>
          <span>02</span>
          <h2>Public hiring surfaces</h2>
          <p>Publish jobs, capture applications, and move candidates from public discovery into structured internal workflows.</p>
        </article>
        <article>
          <span>03</span>
          <h2>Tenant-safe data operations</h2>
          <p>Support multiple staffing firms on the same platform while keeping data strictly isolated at the application layer.</p>
        </article>
        <article>
          <span>04</span>
          <h2>Future-ready staffing ops</h2>
          <p>Lay the foundation for timesheets, invoicing, recruiter portals, and client-facing reporting without rebuilding the core.</p>
        </article>
      </section>
    </main>
  `,
  styles: `
    .page { width: min(1120px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero { max-width: 50rem; margin-bottom: 2rem; }
    .eyebrow { margin: 0 0 0.7rem; color: #9a3412; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0; font-size: clamp(2.2rem, 3.6vw, 4.2rem); line-height: 1.02; }
    .services-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1.25rem; }
    article { padding: 1.6rem; border-radius: 1.5rem; background: linear-gradient(180deg, rgb(255 251 244 / 0.94), rgb(251 243 230 / 0.9)); border: 1px solid rgb(87 70 42 / 0.14); box-shadow: 0 1rem 2rem rgb(64 47 22 / 0.06); }
    span { display: inline-block; color: #9a3412; font-weight: 800; letter-spacing: 0.08em; margin-bottom: 0.8rem; }
    h2 { margin: 0 0 0.75rem; font-size: 1.3rem; }
    p { margin: 0; color: #5a5044; line-height: 1.7; }
    @media (max-width: 860px) { .services-grid { grid-template-columns: 1fr; } }
  `,
})
export class ServicesComponent {}
