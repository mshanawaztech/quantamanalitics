import { Component, computed, inject, signal } from '@angular/core';
import {
  ActivatedRoute,
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { filter } from 'rxjs/operators';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import { NotificationsBellComponent } from './core/notifications/notifications-bell.component';
import { TenantBootstrapBannerComponent } from './core/auth/tenant-bootstrap-banner.component';
import { QaLogoComponent } from './core/ui';

interface Crumb {
  label: string;
  href: string;
}

interface NavItem {
  href: string;
  label: string;
  exact?: boolean;
  authedOnly?: boolean;
  visibility?: 'recruiting' | 'interviews' | 'approvals' | 'candidate' | 'authenticated' | 'admin';
}

const PRIMARY_NAV: NavItem[] = [
  { href: '/', label: 'Home', exact: true },
  { href: '/about', label: 'About' },
  { href: '/services', label: 'Services' },
  { href: '/jobs', label: 'Jobs' },
  { href: '/contact', label: 'Contact' },
];

const PORTAL_NAV: NavItem[] = [
  { href: '/recruiter', label: 'Recruiter', authedOnly: true, visibility: 'recruiting' },
  { href: '/recruiter/pipeline', label: 'Pipeline', authedOnly: true, visibility: 'recruiting' },
  { href: '/recruiter/email-templates', label: 'Templates', authedOnly: true, visibility: 'recruiting' },
  { href: '/onboarding', label: 'Onboarding', authedOnly: true, visibility: 'recruiting' },
  { href: '/client', label: 'Client', authedOnly: true, visibility: 'approvals' },
  { href: '/candidate', label: 'Candidate', authedOnly: true, visibility: 'candidate' },
  { href: '/onboarding/me', label: 'My onboarding', authedOnly: true, visibility: 'authenticated' },
  { href: '/contractor', label: 'Contractor', authedOnly: true, visibility: 'authenticated' },
  { href: '/contractor/invoices', label: 'Invoices', authedOnly: true, visibility: 'authenticated' },
  { href: '/interviews', label: 'Interviews', authedOnly: true, visibility: 'interviews' },
  { href: '/admin/audit', label: 'Admin', authedOnly: true, visibility: 'admin' },
  { href: '/settings/branding', label: 'Branding', authedOnly: true, visibility: 'authenticated' },
];

const ROUTE_LABELS: Record<string, string> = {
  '': 'Home',
  about: 'About',
  services: 'Services',
  jobs: 'Jobs',
  contact: 'Contact',
  recruiter: 'Recruiter',
  pipeline: 'Pipeline',
  'email-templates': 'Email templates',
  onboarding: 'Onboarding',
  client: 'Client',
  candidate: 'Candidate',
  contractor: 'Contractor',
  invoices: 'Invoices',
  interviews: 'Interviews',
  admin: 'Admin',
  audit: 'Audit',
  settings: 'Settings',
  branding: 'Branding',
  'style-guide': 'Style guide',
  trust: 'Trust',
  privacy: 'Privacy',
  terms: 'Terms',
  security: 'Security',
  dpa: 'DPA',
};

/**
 * Phase 6 / Story 46 — global application shell.
 *
 * Provides the persistent visual frame around every routed view:
 *
 *   - Skip-to-main-content link (WCAG 2.4.1)
 *   - Header with logo, primary nav, portal nav (when authenticated),
 *     global search input, and user menu
 *   - Breadcrumbs auto-generated from the current URL
 *   - Footer with sitemap, accessibility link, environment notice
 *   - Mobile hamburger menu (< 768px) that mirrors the desktop nav
 *
 * Replaces the cream / orange ambient marketing-shell from before.
 * The visual identity now matches the design tokens established in
 * Story 45 (white surface, deep-blue accent, Inter typography).
 */
@Component({
  selector: 'app-marketing-shell',
  standalone: true,
  imports: [NotificationsBellComponent, QaLogoComponent, RouterLink, RouterLinkActive, RouterOutlet, TenantBootstrapBannerComponent],
  template: `
    <a class="skip-link" href="#main">Skip to main content</a>

    <div class="shell">
      <header class="shell__header" role="banner">
        <div class="shell__header-inner">
          <a class="brand" routerLink="/" aria-label="Quantam Analytics — home">
            <qa-logo size="36" alt=""></qa-logo>
            <span class="brand__copy">
              <strong>Quantam Analytics</strong>
              <span>Staffing platform</span>
            </span>
          </a>

          <nav class="shell__nav shell__nav--primary" aria-label="Primary">
            @for (item of primaryNav; track item.href) {
              <a
                [routerLink]="item.href"
                routerLinkActive="active"
                [routerLinkActiveOptions]="{ exact: !!item.exact }"
              >{{ item.label }}</a>
            }
          </nav>

          <div class="shell__header-right">
            <form class="search" role="search" (submit)="onSearchSubmit($event)">
              <label class="visually-hidden" for="global-search">Search</label>
              <input
                id="global-search"
                type="search"
                placeholder="Search coming soon"
                [value]="searchQuery()"
                (input)="onSearchInput($event)"
                autocomplete="off"
                disabled
                aria-disabled="true"
                title="Global search isn't wired up yet"
              />
            </form>

            @if (auth.isAuthenticated()) {
              <app-notifications-bell />
              <button
                type="button"
                class="user-menu"
                (click)="toggleUserMenu()"
                [attr.aria-expanded]="userMenuOpen()"
                aria-haspopup="menu"
                aria-label="Account menu"
              >
                <span class="user-menu__avatar" aria-hidden="true">
                  {{ initials() }}
                </span>
                <span class="user-menu__caret" aria-hidden="true">▾</span>
              </button>
              @if (userMenuOpen()) {
                <div class="user-menu__panel" role="menu">
                  <p class="user-menu__email">{{ auth.email() }}</p>
                  @if (access.canAccessCandidatePortal()) {
                    <a routerLink="/candidate" role="menuitem" (click)="closeUserMenu()">My profile</a>
                  }
                  @if (accessBadges().length > 0) {
                    <div class="user-menu__meta">
                      <p class="user-menu__label">Access profile</p>
                      <div class="user-menu__badges">
                        @for (badge of accessBadges(); track badge.key) {
                          <span class="user-menu__badge">{{ badge.label }}</span>
                        }
                      </div>
                    </div>
                  }
                  <button type="button" role="menuitem" (click)="signOut()">Sign out</button>
                </div>
              }
            } @else {
              <a class="cta" routerLink="/login">Sign in</a>
            }

            <button
              type="button"
              class="mobile-toggle"
              (click)="toggleMobileNav()"
              [attr.aria-expanded]="mobileNavOpen()"
              aria-label="Toggle menu"
              aria-controls="mobile-nav"
            >
              <span aria-hidden="true">☰</span>
            </button>
          </div>
        </div>

        @if (auth.isAuthenticated()) {
          <nav class="shell__nav shell__nav--portal" aria-label="Portals">
            <div class="shell__nav-inner">
              @for (item of portalNav(); track item.href) {
                <a [routerLink]="item.href" routerLinkActive="active">{{ item.label }}</a>
              }
            </div>
          </nav>
        }

        @if (mobileNavOpen()) {
          <nav id="mobile-nav" class="shell__mobile-nav" aria-label="Mobile">
            @for (item of primaryNav; track item.href) {
              <a
                [routerLink]="item.href"
                routerLinkActive="active"
                [routerLinkActiveOptions]="{ exact: item.href === '/' }"
                (click)="closeMobileNav()"
              >{{ item.label }}</a>
            }
            @if (auth.isAuthenticated()) {
              <hr />
              @for (item of portalNav(); track item.href) {
                <a
                  [routerLink]="item.href"
                  routerLinkActive="active"
                  (click)="closeMobileNav()"
                >{{ item.label }}</a>
              }
            }
          </nav>
        }
      </header>

      @if (crumbs().length > 1) {
        <nav class="breadcrumbs" aria-label="Breadcrumb">
          <div class="breadcrumbs__inner">
            <ol>
              @for (c of crumbs(); track c.href; let last = $last) {
                <li>
                  @if (!last) {
                    <a [routerLink]="c.href">{{ c.label }}</a>
                    <span class="breadcrumbs__sep" aria-hidden="true">/</span>
                  } @else {
                    <span aria-current="page">{{ c.label }}</span>
                  }
                </li>
              }
            </ol>
          </div>
        </nav>
      }

      <app-tenant-bootstrap-banner />

      <main id="main" class="shell__main" tabindex="-1">
        <router-outlet />
      </main>

      <footer class="shell__footer" role="contentinfo">
        <div class="shell__footer-inner">
          <div class="shell__footer-col">
            <span class="footer-brand">
              <qa-logo size="34" alt=""></qa-logo>
              <strong>Quantam Analytics</strong>
            </span>
            <p>A multi-tenant staffing operating system — sourcing through placement, on one tenant-safe platform.</p>
          </div>
          <div class="shell__footer-col">
            <strong>Product</strong>
            <a routerLink="/jobs">Public jobs</a>
            <a routerLink="/services">Services</a>
            <a routerLink="/about">About</a>
          </div>
          <div class="shell__footer-col">
            <strong>For your team</strong>
            <a routerLink="/recruiter">Recruiter portal</a>
            <a routerLink="/client">Client portal</a>
            <a routerLink="/candidate">Candidate portal</a>
            <a routerLink="/contractor">Contractor portal</a>
          </div>
          <div class="shell__footer-col">
            <strong>Resources</strong>
            <a routerLink="/contact">Contact</a>
            <a routerLink="/accessibility">Accessibility</a>
            <a href="https://github.com/mshanawaztech/quantamanalitics" target="_blank" rel="noopener">GitHub</a>
          </div>
          <div class="shell__footer-col">
            <strong>Trust</strong>
            <a routerLink="/trust">Trust center</a>
            <a routerLink="/privacy">Privacy</a>
            <a routerLink="/terms">Terms</a>
            <a routerLink="/security">Security</a>
            <a routerLink="/dpa">DPA</a>
          </div>
        </div>
        <div class="shell__footer-bottom">
          <span>© {{ year }} Quantam Analytics. All rights reserved.</span>
          <span>Azure-hosted · Auth0-secured · WCAG 2.1 AA</span>
        </div>
      </footer>
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-height: 100vh;
      background: var(--color-canvas);
    }

    .shell {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }

    /* ── Header ──────────────────────────────────────────────────── */

    .shell__header {
      /* Cobalt command bar: deep cobalt gradient, gold hairline on top. */
      background: linear-gradient(180deg, #1733a6 0%, #0f2580 100%);
      border-top: 2px solid #e6c9a8;
      box-shadow: 0 2px 14px rgba(13, 33, 85, 0.22);
      position: sticky;
      top: 0;
      z-index: 50;
    }

    .shell__header-inner {
      max-width: var(--container-max);
      margin: 0 auto;
      padding: var(--space-3) var(--space-5);
      display: flex;
      align-items: center;
      gap: var(--space-5);
    }

    .brand {
      display: inline-flex;
      align-items: center;
      gap: var(--space-3);
      text-decoration: none;
      color: #ffffff;
      flex-shrink: 0;
    }

    .brand qa-logo {
      display: inline-flex;
      line-height: 0;
    }

    .brand__copy {
      display: flex;
      flex-direction: column;
      line-height: 1.15;
    }
    .brand__copy strong {
      font-size: var(--font-size-md);
      font-weight: var(--font-weight-bold);
      color: #ffffff;
      letter-spacing: 0.01em;
    }
    .brand__copy span {
      font-size: var(--font-size-xs);
      color: rgba(255, 255, 255, 0.66);
    }

    /* Primary nav (top row, marketing pages) */
    .shell__nav--primary {
      display: flex;
      gap: var(--space-1);
      flex: 1;
      justify-content: center;
    }

    .shell__nav--primary a {
      padding: var(--space-2) var(--space-4);
      border-radius: var(--radius-pill);
      color: rgba(255, 255, 255, 0.82);
      text-decoration: none;
      font-weight: var(--font-weight-medium);
      font-size: var(--font-size-sm);
      transition: background-color 160ms ease, color 160ms ease;
    }

    .shell__nav--primary a:hover {
      background: rgba(255, 255, 255, 0.12);
      color: #ffffff;
    }

    .shell__nav--primary a.active {
      background: #ffffff;
      color: #0f2580;
      font-weight: var(--font-weight-semi);
    }

    /* Header right cluster */
    .shell__header-right {
      display: flex;
      align-items: center;
      gap: var(--space-3);
      position: relative;
    }

    .search input {
      width: 18rem;
      max-width: 100%;
      padding: var(--space-2) var(--space-3);
      background: rgba(0, 0, 0, 0.22);
      border: 1px solid rgba(255, 255, 255, 0.16);
      border-radius: var(--radius-md);
      color: #ffffff;
      font: inherit;
      font-size: var(--font-size-sm);
      transition: border-color 160ms ease, box-shadow 160ms ease;
    }
    .search input::placeholder { color: rgba(255, 255, 255, 0.6); }
    .search input:focus {
      outline: none;
      border-color: #e6c9a8;
      box-shadow: 0 0 0 3px rgba(230, 201, 168, 0.28);
    }

    .cta {
      padding: var(--space-2) var(--space-4);
      background: #ffffff;
      color: #0f2580;
      border-radius: var(--radius-md);
      text-decoration: none;
      font-weight: var(--font-weight-semi);
      font-size: var(--font-size-sm);
      transition: background-color 160ms ease;
    }
    .cta:hover { background: #e6c9a8; color: #0f2580; }

    /* User menu */
    .user-menu {
      display: inline-flex;
      align-items: center;
      gap: var(--space-2);
      padding: var(--space-1) var(--space-2);
      background: rgba(255, 255, 255, 0.1);
      border: 1px solid rgba(255, 255, 255, 0.22);
      border-radius: var(--radius-pill);
      color: #ffffff;
      cursor: pointer;
      font: inherit;
    }
    .user-menu:hover { background: rgba(255, 255, 255, 0.18); }
    .user-menu__avatar {
      width: 1.75rem; height: 1.75rem;
      display: grid; place-items: center;
      background: #e6c9a8;
      color: #0f2580;
      border-radius: var(--radius-pill);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-bold);
    }
    .user-menu__caret { font-size: var(--font-size-xs); color: rgba(255, 255, 255, 0.8); }

    .user-menu__panel {
      position: absolute;
      top: calc(100% + var(--space-2));
      right: 0;
      min-width: 14rem;
      padding: var(--space-2);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      box-shadow: var(--shadow-lg);
      display: flex;
      flex-direction: column;
      gap: 2px;
      z-index: 60;
    }
    .user-menu__panel a, .user-menu__panel button {
      display: block;
      padding: var(--space-2) var(--space-3);
      background: transparent;
      border: 0;
      border-radius: var(--radius-sm);
      color: var(--color-ink);
      font: inherit;
      text-decoration: none;
      text-align: left;
      cursor: pointer;
    }
    .user-menu__panel a:hover, .user-menu__panel button:hover {
      background: var(--color-primary-soft);
    }
    .user-menu__email {
      margin: 0;
      padding: var(--space-2) var(--space-3);
      color: var(--color-ink-muted);
      font-size: var(--font-size-xs);
      border-bottom: 1px solid var(--color-border);
    }
    .user-menu__meta {
      padding: var(--space-2) var(--space-3);
      border-top: 1px solid var(--color-border);
      border-bottom: 1px solid var(--color-border);
      display: grid;
      gap: var(--space-2);
    }
    .user-menu__label {
      margin: 0;
      color: var(--color-ink-muted);
      font-size: var(--font-size-xs);
    }
    .user-menu__badges {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-1);
    }
    .user-menu__badge {
      display: inline-flex;
      align-items: center;
      padding: 0.25rem 0.55rem;
      border-radius: var(--radius-pill);
      background: var(--color-primary-soft);
      color: var(--color-primary);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-bold);
    }

    .mobile-toggle {
      display: none;
      width: 2.5rem; height: 2.5rem;
      background: rgba(255, 255, 255, 0.1);
      border: 1px solid rgba(255, 255, 255, 0.22);
      border-radius: var(--radius-md);
      color: #ffffff;
      font-size: var(--font-size-lg);
      cursor: pointer;
    }

    /* Portal nav (second row) */
    .shell__nav--portal {
      background: var(--color-surface-alt);
      border-top: 1px solid var(--color-border);
    }
    .shell__nav-inner {
      max-width: var(--container-max);
      margin: 0 auto;
      padding: var(--space-2) var(--space-5);
      display: flex;
      gap: var(--space-2);
      overflow-x: auto;
    }
    .shell__nav--portal a {
      padding: var(--space-2) var(--space-3);
      color: var(--color-ink-muted);
      text-decoration: none;
      font-weight: var(--font-weight-medium);
      font-size: var(--font-size-sm);
      border-radius: var(--radius-md);
      white-space: nowrap;
      transition: color 160ms ease, background-color 160ms ease;
    }
    .shell__nav--portal a:hover { color: var(--color-primary); }
    .shell__nav--portal a.active {
      color: var(--color-primary);
      background: var(--color-surface);
      box-shadow: inset 0 -2px 0 var(--color-primary);
    }

    /* Mobile sliding menu */
    .shell__mobile-nav {
      display: none;
      flex-direction: column;
      padding: var(--space-2) var(--space-5);
      background: var(--color-surface);
      border-top: 1px solid var(--color-border);
    }
    .shell__mobile-nav a {
      padding: var(--space-3) var(--space-2);
      color: var(--color-ink);
      text-decoration: none;
      font-weight: var(--font-weight-medium);
      border-bottom: 1px solid var(--color-border);
    }
    .shell__mobile-nav a:last-child { border-bottom: 0; }
    .shell__mobile-nav hr {
      margin: var(--space-2) 0;
      border: 0;
      border-top: 1px solid var(--color-border-strong);
    }

    /* ── Breadcrumbs ─────────────────────────────────────────────── */

    .breadcrumbs {
      background: var(--color-canvas);
      border-bottom: 1px solid var(--color-border);
    }
    .breadcrumbs__inner {
      max-width: var(--container-max);
      margin: 0 auto;
      padding: var(--space-2) var(--space-5);
    }
    .breadcrumbs ol {
      list-style: none;
      margin: 0; padding: 0;
      display: flex; flex-wrap: wrap;
      gap: var(--space-1);
      font-size: var(--font-size-xs);
      color: var(--color-ink-muted);
    }
    .breadcrumbs li { display: inline-flex; align-items: center; gap: var(--space-1); }
    .breadcrumbs a { color: var(--color-primary); text-decoration: none; }
    .breadcrumbs a:hover { text-decoration: underline; }
    .breadcrumbs__sep { color: var(--color-border-strong); }
    .breadcrumbs [aria-current='page'] {
      color: var(--color-ink);
      font-weight: var(--font-weight-semi);
    }

    /* ── Main ────────────────────────────────────────────────────── */

    .shell__main {
      flex: 1;
      max-width: var(--container-max);
      width: 100%;
      margin: 0 auto;
      padding: var(--space-5);
    }
    .shell__main:focus { outline: none; }

    /* ── Footer ──────────────────────────────────────────────────── */

    .shell__footer {
      background: var(--color-ink-strong);
      color: var(--color-ink-onblue);
    }
    .shell__footer-inner {
      max-width: var(--container-max);
      margin: 0 auto;
      padding: var(--space-7) var(--space-5) var(--space-5);
      display: grid;
      gap: var(--space-5);
      grid-template-columns: 1.4fr repeat(3, 1fr);
    }
    .shell__footer-col { display: flex; flex-direction: column; gap: var(--space-2); }
    .footer-brand { display: inline-flex; align-items: center; gap: var(--space-2); margin-bottom: var(--space-1); }
    .footer-brand strong { margin-bottom: 0; text-transform: none; letter-spacing: 0; font-size: var(--font-size-md); }
    .shell__footer-col strong {
      font-size: var(--font-size-sm);
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--color-ink-onblue);
      margin-bottom: var(--space-1);
    }
    .shell__footer-col a {
      color: rgba(255 255 255 / 0.78);
      text-decoration: none;
      font-size: var(--font-size-sm);
    }
    .shell__footer-col a:hover { color: var(--color-ink-onblue); text-decoration: underline; }
    .shell__footer-col p {
      color: rgba(255 255 255 / 0.78);
      font-size: var(--font-size-sm);
      margin: 0;
    }
    .shell__footer-bottom {
      max-width: var(--container-max);
      margin: 0 auto;
      padding: var(--space-3) var(--space-5);
      display: flex; justify-content: space-between; flex-wrap: wrap;
      gap: var(--space-2);
      border-top: 1px solid rgba(255 255 255 / 0.16);
      color: rgba(255 255 255 / 0.62);
      font-size: var(--font-size-xs);
    }

    /* ── Responsive ──────────────────────────────────────────────── */

    @media (max-width: 1024px) {
      .search input { width: 12rem; }
      .shell__footer-inner { grid-template-columns: 1fr 1fr; }
    }

    @media (max-width: 768px) {
      .shell__nav--primary,
      .shell__nav--portal,
      .search,
      .user-menu,
      .cta { display: none; }
      .mobile-toggle { display: inline-grid; place-items: center; }
      .shell__mobile-nav { display: flex; }
      .shell__footer-inner { grid-template-columns: 1fr; }
      .shell__footer-bottom { flex-direction: column; }
    }
  `,
})
export class MarketingShellComponent {
  protected auth = inject(AuthService);
  protected access = inject(AccessService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  protected readonly primaryNav = PRIMARY_NAV;
  protected readonly portalNav = computed(() =>
    PORTAL_NAV.filter((item) => this.canShowPortalItem(item)),
  );
  protected readonly accessBadges = this.access.accessBadges;
  protected readonly year = new Date().getFullYear();

  protected readonly searchQuery = signal('');
  protected readonly mobileNavOpen = signal(false);
  protected readonly userMenuOpen = signal(false);

  protected readonly currentUrl = signal(this.router.url);
  protected readonly crumbs = computed<Crumb[]>(() => this.buildCrumbs(this.currentUrl()));

  protected readonly initials = computed(() => {
    const email = this.auth.email() ?? '';
    return email.length > 0 ? email[0].toUpperCase() : '·';
  });

  constructor() {
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => {
        this.currentUrl.set(e.urlAfterRedirects);
        this.mobileNavOpen.set(false);
        this.userMenuOpen.set(false);
      });
  }

  protected onSearchInput(event: Event) {
    this.searchQuery.set((event.target as HTMLInputElement).value);
  }

  protected onSearchSubmit(event: Event) {
    event.preventDefault();
    // Phase 6 ships UI only — wiring to a real cross-entity search
    // endpoint lives in Phase 7. For now the form just blurs the input.
    (event.target as HTMLFormElement).blur();
  }

  protected toggleMobileNav() { this.mobileNavOpen.update(v => !v); }
  protected closeMobileNav() { this.mobileNavOpen.set(false); }

  protected toggleUserMenu() { this.userMenuOpen.update(v => !v); }
  protected closeUserMenu() { this.userMenuOpen.set(false); }

  protected signOut() {
    this.closeUserMenu();
    this.auth.logout();
  }

  private canShowPortalItem(item: NavItem): boolean {
    if (!item.authedOnly) {
      return true;
    }

    if (!this.auth.isAuthenticated()) {
      return false;
    }

    switch (item.visibility) {
      case 'recruiting':
        return this.access.canAccessRecruitingWorkspace();
      case 'interviews':
        return this.access.canAccessInterviewWorkspace();
      case 'approvals':
        return this.access.canAccessTimeApproval();
      case 'candidate':
        return this.access.canAccessCandidatePortal();
      case 'admin':
        return this.access.isPlatformAdmin();
      case 'authenticated':
      default:
        return true;
    }
  }

  private buildCrumbs(url: string): Crumb[] {
    const clean = url.split('?')[0].split('#')[0];
    const parts = clean.split('/').filter(p => p.length > 0);
    const crumbs: Crumb[] = [{ label: 'Home', href: '/' }];

    let acc = '';
    for (const part of parts) {
      acc += '/' + part;
      const label = ROUTE_LABELS[part] ?? this.titleCase(decodeURIComponent(part));
      crumbs.push({ label, href: acc });
    }
    return crumbs;
  }

  private titleCase(s: string): string {
    return s.replace(/[-_]/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
  }
}
