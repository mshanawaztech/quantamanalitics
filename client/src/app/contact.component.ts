import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-contact',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="page">
      <header class="hero">
        <p class="eyebrow">Contact</p>
        <h1>Talk through your staffing workflow.</h1>
        <p class="lede">
          Quantam Analytics is shaped around real recruiting operations.
          Reach out if you want to discuss internal ATS workflow, recruiter
          collaboration, contractor timesheets and invoicing, client
          approval flows, or the SaaS product roadmap.
        </p>
      </header>

      <section class="grid">
        <article class="card card--primary">
          <h2>General &amp; sales</h2>
          <p>Product questions, demos, custom-tenant onboarding.</p>
          <a href="mailto:hello@quantamanalitics.com">hello&#64;quantamanalitics.com</a>
        </article>
        <article class="card">
          <h2>Security</h2>
          <p>Vulnerability reports, security questionnaires, DPA requests.</p>
          <a href="mailto:security@quantamanalitics.com">security&#64;quantamanalitics.com</a>
          <a routerLink="/security" class="secondary-link">Security overview →</a>
        </article>
        <article class="card">
          <h2>Privacy</h2>
          <p>Data access / correction / deletion under GDPR or CCPA.</p>
          <a href="mailto:privacy@quantamanalitics.com">privacy&#64;quantamanalitics.com</a>
          <a routerLink="/privacy" class="secondary-link">Privacy notice →</a>
        </article>
        <article class="card">
          <h2>Engineering</h2>
          <p>Bug reports, integration questions, API onboarding.</p>
          <a href="mailto:engineering@quantamanalitics.com">engineering&#64;quantamanalitics.com</a>
          <a href="https://github.com/mshanawaz114/quantamanalitics" target="_blank" rel="noopener" class="secondary-link">GitHub →</a>
        </article>
      </section>

      <aside class="card card--meta">
        <h2>Try the product first</h2>
        <p>
          The fastest answer to "do you have X?" is usually "open a portal."
          Five public portal entries are below — sign in (or use the
          shared dev tenant) and click around.
        </p>
        <div class="portal-links">
          <a routerLink="/recruiter">Recruiter</a>
          <a routerLink="/contractor">Contractor</a>
          <a routerLink="/client">Client</a>
          <a routerLink="/candidate">Candidate</a>
          <a routerLink="/interviews">Interviews</a>
          <a routerLink="/admin/audit">Admin</a>
        </div>
      </aside>
    </main>
  `,
  styles: `
    :host { display: block; }
    .page { width: min(1040px, 100%); margin: 0 auto; padding: 2rem 1.5rem 4rem; }
    .eyebrow { margin: 0 0 0.5rem; color: var(--color-primary, #1a3a8f); text-transform: uppercase; letter-spacing: 0.12em; font-size: 12px; font-weight: 700; }
    .hero h1 { margin: 0 0 0.75rem; font-size: clamp(2rem, 3vw, 2.5rem); line-height: 1.1; letter-spacing: -0.02em; }
    .hero .lede { color: var(--color-fg-muted, #5d6577); max-width: 60ch; margin: 0 0 2rem; font-size: 1.05rem; line-height: 1.65; }

    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 1rem;
      margin-bottom: 2rem;
    }
    .card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 12px;
      padding: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .card--primary {
      background: var(--color-primary, #1a3a8f);
      color: #fff;
    }
    .card--primary a { color: #fff; }
    .card h2 { margin: 0; font-size: 1.05rem; }
    .card p { margin: 0; line-height: 1.55; color: var(--color-fg-muted, #5d6577); }
    .card--primary p { color: rgba(255, 255, 255, 0.85); }
    .card a {
      color: var(--color-primary, #1a3a8f);
      font-weight: 600;
      text-decoration: none;
    }
    .card a:hover { text-decoration: underline; }
    .card .secondary-link {
      font-size: 0.85rem;
      font-weight: 500;
      color: var(--color-fg-muted, #5d6577);
    }
    .card--primary .secondary-link { color: rgba(255, 255, 255, 0.75); }

    .card--meta {
      border-style: dashed;
    }
    .portal-links {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin-top: 0.5rem;
    }
    .portal-links a {
      display: inline-block;
      padding: 0.4rem 0.875rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 999px;
      font-size: 0.875rem;
      font-weight: 600;
      text-decoration: none;
      color: var(--color-primary, #1a3a8f);
    }
    .portal-links a:hover { background: var(--color-primary-soft, #e7ecf6); }

    @media (max-width: 720px) { .grid { grid-template-columns: 1fr; } }
  `,
})
export class ContactComponent {}
