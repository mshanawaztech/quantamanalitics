import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-marketing-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="site-shell">
      <div class="ambient ambient-left"></div>
      <div class="ambient ambient-right"></div>

      <header class="site-header">
        <a class="brand" routerLink="/">
          <span class="brand-mark">QA</span>
          <span class="brand-copy">
            <strong>Quantam Analytics</strong>
            <span>Staffing platform for modern recruiting teams</span>
          </span>
        </a>

        <nav class="site-nav" aria-label="Primary">
          <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Home</a>
          <a routerLink="/about" routerLinkActive="active">About</a>
          <a routerLink="/services" routerLinkActive="active">Services</a>
          <a routerLink="/contact" routerLinkActive="active">Contact</a>
        </nav>
      </header>

      <router-outlet />

      <footer class="site-footer">
        <p>Quantam Analytics is building a multi-tenant staffing operating system from sourcing to submission.</p>
        <div>
          <a routerLink="/contact">Talk to us</a>
          <span>Azure-hosted · Auth0-secured · SaaS-ready</span>
        </div>
      </footer>
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-height: 100vh;
    }

    .site-shell {
      position: relative;
      min-height: 100vh;
      padding: 1.5rem;
      overflow: hidden;
      background:
        radial-gradient(circle at top left, rgb(251 191 36 / 0.24), transparent 24rem),
        radial-gradient(circle at top right, rgb(14 165 233 / 0.20), transparent 22rem),
        linear-gradient(180deg, #f8f4eb 0%, #fffdf9 42%, #f5efe1 100%);
      color: #1f1d1a;
    }

    .ambient {
      position: absolute;
      border-radius: 999px;
      filter: blur(48px);
      opacity: 0.6;
      pointer-events: none;
    }

    .ambient-left {
      inset: 5rem auto auto -5rem;
      width: 14rem;
      height: 14rem;
      background: rgb(180 83 9 / 0.16);
    }

    .ambient-right {
      inset: 18rem -4rem auto auto;
      width: 18rem;
      height: 18rem;
      background: rgb(3 105 161 / 0.14);
    }

    .site-header,
    .site-footer,
    router-outlet {
      position: relative;
      z-index: 1;
    }

    .site-header,
    .site-footer {
      width: min(1120px, 100%);
      margin: 0 auto;
    }

    .site-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
      padding: 0.5rem 0 1.75rem;
    }

    .brand {
      display: inline-flex;
      gap: 0.9rem;
      align-items: center;
      text-decoration: none;
      color: inherit;
    }

    .brand-mark {
      display: inline-grid;
      place-items: center;
      width: 2.85rem;
      height: 2.85rem;
      border-radius: 0.9rem;
      background: linear-gradient(145deg, #111827, #9a3412);
      color: #fff7ed;
      font-weight: 800;
      letter-spacing: 0.08em;
    }

    .brand-copy {
      display: grid;
      gap: 0.15rem;
    }

    .brand-copy strong {
      font-size: 1rem;
    }

    .brand-copy span:last-child {
      color: #6b6255;
      font-size: 0.92rem;
    }

    .site-nav {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem;
      padding: 0.35rem;
      border: 1px solid rgb(111 100 84 / 0.18);
      border-radius: 999px;
      background: rgb(255 253 249 / 0.78);
      backdrop-filter: blur(12px);
    }

    .site-nav a {
      padding: 0.7rem 1rem;
      border-radius: 999px;
      color: #554b3d;
      text-decoration: none;
      font-size: 0.95rem;
      font-weight: 600;
      transition: background-color 160ms ease, color 160ms ease, transform 160ms ease;
    }

    .site-nav a:hover,
    .site-nav a.active {
      background: #1f2937;
      color: #fff8ee;
      transform: translateY(-1px);
    }

    .site-footer {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      padding: 2.25rem 0 1rem;
      color: #6b6255;
      font-size: 0.94rem;
    }

    .site-footer div {
      display: flex;
      gap: 1rem;
      flex-wrap: wrap;
      justify-content: flex-end;
    }

    .site-footer a {
      color: #9a3412;
      font-weight: 700;
      text-decoration: none;
    }

    @media (max-width: 900px) {
      .site-header,
      .site-footer {
        flex-direction: column;
        align-items: flex-start;
      }

      .site-footer div {
        justify-content: flex-start;
      }
    }
  `,
})
export class MarketingShellComponent {}
