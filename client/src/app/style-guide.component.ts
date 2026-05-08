import { Component } from '@angular/core';
import {
  QaAlertComponent,
  QaBadgeComponent,
  QaButtonComponent,
  QaCardComponent,
  QaEmptyStateComponent,
  QaInputComponent,
} from './core/ui';

/**
 * /style-guide — visual reference for every design-system primitive.
 * Used to verify contrast, focus states, spacing, and typography in
 * one place during Phase 6.
 */
@Component({
  selector: 'app-style-guide',
  standalone: true,
  imports: [
    QaAlertComponent,
    QaBadgeComponent,
    QaButtonComponent,
    QaCardComponent,
    QaEmptyStateComponent,
    QaInputComponent,
  ],
  template: `
    <main id="main" class="page">
      <header class="page__head">
        <p class="eyebrow">Design system · v1</p>
        <h1>Quantam Analytics — Style guide</h1>
        <p class="lede">
          Every primitive used across the platform. Colors are WCAG 2.1 AA
          compliant; focus rings are 3px and visible everywhere; typography
          scales smoothly from 320px to 1440px wide.
        </p>
      </header>

      <section class="section">
        <h2>Color</h2>
        <div class="swatches">
          <div class="swatch" style="background:var(--color-primary);color:var(--color-ink-onblue)">
            <strong>Primary</strong>
            <code>#1a3a8f</code>
          </div>
          <div class="swatch" style="background:var(--color-primary-hover);color:var(--color-ink-onblue)">
            <strong>Primary hover</strong>
            <code>#0d2766</code>
          </div>
          <div class="swatch" style="background:var(--color-canvas);color:var(--color-ink)">
            <strong>Canvas</strong>
            <code>#f5f7fb</code>
          </div>
          <div class="swatch" style="background:var(--color-surface);color:var(--color-ink);border:1px solid var(--color-border)">
            <strong>Surface</strong>
            <code>#ffffff</code>
          </div>
          <div class="swatch" style="background:var(--color-success-soft);color:var(--color-success)">
            <strong>Success</strong>
            <code>#146c43</code>
          </div>
          <div class="swatch" style="background:var(--color-warning-soft);color:var(--color-warning)">
            <strong>Warning</strong>
            <code>#944b00</code>
          </div>
          <div class="swatch" style="background:var(--color-danger-soft);color:var(--color-danger)">
            <strong>Danger</strong>
            <code>#b02a37</code>
          </div>
        </div>
      </section>

      <section class="section">
        <h2>Typography</h2>
        <div class="type-stack">
          <p class="display">Display · 48 / 56</p>
          <h1>Heading 1 · 40 / 48</h1>
          <h2>Heading 2 · 32 / 40</h2>
          <h3>Heading 3 · 24 / 32</h3>
          <h4>Heading 4 · 18 / 28</h4>
          <p>Body · 16 / 24. The quick brown fox jumps over the lazy dog.</p>
          <p style="font-size:var(--font-size-sm)">Small · 14 / 20.</p>
          <p style="font-size:var(--font-size-xs);color:var(--color-ink-muted)">Caption · 12 / 16.</p>
        </div>
      </section>

      <section class="section">
        <h2>Buttons</h2>
        <div class="row">
          <qa-button variant="primary">Primary action</qa-button>
          <qa-button variant="secondary">Secondary</qa-button>
          <qa-button variant="ghost">Ghost</qa-button>
          <qa-button variant="destructive">Destructive</qa-button>
          <qa-button variant="primary" [disabled]="true">Disabled</qa-button>
        </div>
      </section>

      <section class="section">
        <h2>Inputs</h2>
        <div class="grid-2">
          <qa-input label="Email address" type="email" placeholder="you@example.com" hint="We never share email addresses." />
          <qa-input label="Required field" [required]="true" placeholder="Type here" />
          <qa-input label="Field with error" error="That value is already in use." value="taken@example.com" />
          <qa-input label="Disabled" [disabled]="true" value="Disabled value" />
        </div>
      </section>

      <section class="section">
        <h2>Badges</h2>
        <div class="row">
          <qa-badge tone="neutral">Neutral</qa-badge>
          <qa-badge tone="info">Info</qa-badge>
          <qa-badge tone="primary">Primary</qa-badge>
          <qa-badge tone="success">Approved</qa-badge>
          <qa-badge tone="warning">Pending</qa-badge>
          <qa-badge tone="danger">Rejected</qa-badge>
        </div>
      </section>

      <section class="section">
        <h2>Alerts</h2>
        <div class="stack">
          <qa-alert tone="info" title="Heads up">
            Phase 6 is in flight — every screen will pick up this design system.
          </qa-alert>
          <qa-alert tone="success" title="All set">
            Your changes were saved.
          </qa-alert>
          <qa-alert tone="warning" title="Action needed">
            Two timesheets are still in draft for last week.
          </qa-alert>
          <qa-alert tone="danger" title="Something went wrong">
            We couldn't reach the audit log service. The save was rolled back.
          </qa-alert>
        </div>
      </section>

      <section class="section">
        <h2>Card</h2>
        <div class="grid-2">
          <qa-card eyebrow="Live API" title="Production status" subtitle="Updated just now">
            <p>Card body content. Pass <code>title</code>, <code>subtitle</code>, and <code>eyebrow</code> as inputs, or use the <code>card-header</code> slot for a custom header.</p>
          </qa-card>
          <qa-card [interactive]="true" title="Interactive card" subtitle="Hover or focus to see the lift">
            <p>Set <code>interactive</code> to elevate on hover.</p>
          </qa-card>
        </div>
      </section>

      <section class="section">
        <h2>Empty state</h2>
        <qa-empty-state
          title="No interviews scheduled"
          description="Once a recruiter schedules an interview for one of your candidates, it'll appear here with the time, link, and panel members."
          icon="📅"
        />
      </section>
    </main>
  `,
  styles: `
    :host { display: block; }

    .page {
      max-width: var(--container-max);
      margin: 0 auto;
      padding: var(--space-7) var(--space-5);
    }

    .page__head { margin-bottom: var(--space-7); }

    .eyebrow {
      margin: 0 0 var(--space-2);
      color: var(--color-primary);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semi);
      text-transform: uppercase;
      letter-spacing: 0.08em;
    }

    .lede {
      max-width: 60ch;
      color: var(--color-ink-muted);
      font-size: var(--font-size-lg);
      line-height: var(--line-height-loose);
    }

    .section {
      padding: var(--space-6) 0;
      border-top: 1px solid var(--color-border);
    }

    .display {
      font-size: var(--font-size-4xl);
      line-height: var(--line-height-tight);
      font-weight: var(--font-weight-bold);
      color: var(--color-ink-strong);
      margin: 0 0 var(--space-3);
    }

    .row {
      display: flex; flex-wrap: wrap; gap: var(--space-3);
      align-items: center;
    }

    .stack {
      display: flex; flex-direction: column; gap: var(--space-3);
    }

    .grid-2 {
      display: grid; gap: var(--space-4);
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    }

    .swatches {
      display: grid; gap: var(--space-3);
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
    }
    .swatch {
      padding: var(--space-5);
      border-radius: var(--radius-md);
      display: flex; flex-direction: column; gap: var(--space-1);
    }
    .swatch strong { font-size: var(--font-size-sm); }
    .swatch code { font-size: var(--font-size-xs); font-family: var(--font-mono); }

    .type-stack { display: flex; flex-direction: column; gap: var(--space-2); }
  `,
})
export class StyleGuideComponent {}
