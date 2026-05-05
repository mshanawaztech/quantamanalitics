import { Component } from '@angular/core';

@Component({
  selector: 'app-about',
  template: `
    <main class="page">
      <section class="hero">
        <p class="eyebrow">About Quantam Analytics</p>
        <h1>Built for staffing firms that need a system, not another spreadsheet stack.</h1>
        <p class="lede">
          We are shaping Quantam Analytics as a multi-tenant staffing platform that
          brings sourcing, screening, submissions, and client coordination into one
          operational surface.
        </p>
      </section>

      <section class="story-grid">
        <article>
          <h2>Why it exists</h2>
          <p>
            Staffing teams often work across fragmented tools: ATS data in one place,
            outreach in another, interview notes in chat, and submissions tracked in
            spreadsheets. That creates lag, duplicated work, and zero trustworthy
            visibility across recruiters.
          </p>
        </article>
        <article>
          <h2>What makes it different</h2>
          <p>
            Quantam Analytics is being designed as SaaS from day one, with
            tenant-isolated data, role-based access, and public-facing candidate
            experiences living in the same product foundation.
          </p>
        </article>
      </section>
    </main>
  `,
  styles: `
    .page { width: min(1120px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero { max-width: 54rem; }
    .eyebrow { margin: 0 0 0.7rem; color: #9a3412; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0; font-size: clamp(2.4rem, 4vw, 4.6rem); line-height: 0.96; }
    .lede { max-width: 42rem; margin-top: 1.1rem; color: #5b5247; font-size: 1.14rem; line-height: 1.7; }
    .story-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1.25rem; margin-top: 2.25rem; }
    article { padding: 1.6rem; border-radius: 1.5rem; background: rgb(255 251 244 / 0.85); border: 1px solid rgb(87 70 42 / 0.14); }
    h2 { margin-top: 0; font-size: 1.3rem; }
    p { color: #4d453b; line-height: 1.75; }
    @media (max-width: 860px) { .story-grid { grid-template-columns: 1fr; } }
  `,
})
export class AboutComponent {}
