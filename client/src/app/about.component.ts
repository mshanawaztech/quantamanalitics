import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-about',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="page">
      <header class="hero">
        <p class="eyebrow">About Quantam Analytics</p>
        <h1>Built for staffing firms that need a system, not another spreadsheet stack.</h1>
        <p class="lede">
          Quantam Analytics is a multi-tenant staffing platform that brings
          sourcing, screening, submissions, contractor timesheets, client
          approvals, and back-office reporting into one operational surface.
          One sign-in, one tenant-safe data layer, five role-specific
          portals.
        </p>
      </header>

      <section class="story-grid">
        <article>
          <h2>Why it exists</h2>
          <p>
            Staffing teams work across fragmented tools — ATS data in one
            place, outreach in another, timesheets emailed as PDFs, invoices
            tracked in a spreadsheet a single person owns. The result is
            lag, duplicated work, and zero trustworthy visibility across
            recruiters or clients.
          </p>
        </article>
        <article>
          <h2>What's different</h2>
          <p>
            The platform is multi-tenant from day one. Every database read
            is clamped to the current tenant via a global query filter, every
            mutation lands in an append-only audit log, and every portal
            sits behind a single role-aware sign-in. The same codebase
            serves the firm, their clients, their candidates, and their
            contractors — without any of them seeing each other's data.
          </p>
        </article>
        <article>
          <h2>How it ships</h2>
          <p>
            One PR per deliverable, an eight-PR phase board, a public
            squash-merge history. The roadmap is on the
            <a routerLink="/trust">trust center</a>; the running
            engineering proof — live API, Auth0, tenant context — sits on
            the <a routerLink="/">home page</a>.
          </p>
        </article>
        <article>
          <h2>Who runs it</h2>
          <p>
            Owned by Shahnawaz Mohammed. Domain:
            <code>quantamanalitics.com</code>. Source on
            <a href="https://github.com/mshanawaz114/quantamanalitics" target="_blank" rel="noopener">GitHub</a>.
            Reach the team via the
            <a routerLink="/contact">contact page</a>.
          </p>
        </article>
      </section>
    </main>
  `,
  styles: `
    .page { width: min(1040px, 100%); margin: 0 auto; padding: 2rem 1.5rem 4rem; }
    .eyebrow { margin: 0 0 0.5rem; color: var(--color-primary, #1a3a8f); text-transform: uppercase; letter-spacing: 0.12em; font-size: 12px; font-weight: 700; }
    .hero { margin-bottom: 2rem; }
    h1 { margin: 0 0 0.75rem; font-size: clamp(2rem, 3vw, 2.5rem); line-height: 1.1; letter-spacing: -0.02em; }
    .lede { max-width: 60ch; margin: 0; color: var(--color-fg-muted, #5d6577); font-size: 1.05rem; line-height: 1.65; }
    .story-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 1rem;
    }
    article {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 12px;
      padding: 1.5rem;
    }
    h2 { margin: 0 0 0.5rem; font-size: 1.1rem; color: var(--color-primary, #1a3a8f); }
    article p { margin: 0; color: var(--color-fg, #102046); line-height: 1.6; }
    article code { background: var(--color-canvas, #f5f7fb); padding: 0.1rem 0.35rem; border-radius: 4px; font-size: 0.85em; }
    article a { color: var(--color-primary, #1a3a8f); text-decoration: underline; }
    @media (max-width: 720px) { .story-grid { grid-template-columns: 1fr; } }
  `,
})
export class AboutComponent {}
