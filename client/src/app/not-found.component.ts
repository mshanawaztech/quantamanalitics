import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <main class="page">
      <section class="card">
        <p class="eyebrow">404</p>
        <h1>Page not found</h1>
        <p>
          This route does not exist in the current Quantam Analytics preview.
          Use one of the live entry points below to keep exploring the product.
        </p>
        <div class="actions">
          <a routerLink="/">Back home</a>
          <a routerLink="/jobs">Browse jobs</a>
          <a routerLink="/contact">Contact us</a>
        </div>
      </section>
    </main>
  `,
  styles: `
    .page { width: min(1120px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .card {
      max-width: 44rem;
      padding: 2rem;
      border-radius: 1.5rem;
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
      box-shadow: var(--shadow-md);
    }
    .eyebrow { margin: 0 0 0.7rem; color: var(--color-primary, #1a3a8f); text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.8rem; font-size: clamp(2.1rem, 3.6vw, 4rem); line-height: 0.98; }
    p { color: var(--color-ink-muted, #4b5a72); line-height: 1.75; }
    .actions { display: flex; gap: 0.8rem; flex-wrap: wrap; margin-top: 1rem; }
    a {
      padding: 0.85rem 1rem;
      border-radius: 999px;
      background: var(--color-primary, #1a3a8f);
      color: var(--color-ink-onblue, #ffffff);
      font-weight: 700;
      text-decoration: none;
    }
  `,
})
export class NotFoundComponent {}
