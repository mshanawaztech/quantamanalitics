import { Component } from '@angular/core';

/**
 * /accessibility — public accessibility statement linked from the footer.
 * Required by ADA / WCAG good-practice; gives users a way to flag issues
 * and tells them what conformance level we target.
 */
@Component({
  selector: 'app-accessibility',
  standalone: true,
  template: `
    <main id="main" class="page" tabindex="-1">
      <header class="page__head">
        <p class="eyebrow">Quantam Analytics</p>
        <h1>Accessibility statement</h1>
        <p class="lede">
          We design Quantam Analytics so it works for people who use
          assistive technology, who navigate by keyboard alone, who rely
          on screen readers, and who need higher contrast or larger text.
          This page records the conformance level we target, the
          mechanisms we use to maintain it, and how to reach us if you
          run into a barrier.
        </p>
      </header>

      <section>
        <h2>Conformance target</h2>
        <p>
          The platform targets <strong>WCAG 2.1 Level AA</strong>. Where
          a feature falls short of that target during active development,
          we surface the gap on this page and a deliverable on the active
          phase board.
        </p>
      </section>

      <section>
        <h2>Mechanisms</h2>
        <ul>
          <li>Visible focus indication on every interactive element.</li>
          <li>Skip-to-main-content link revealed on keyboard navigation.</li>
          <li>Programmatic labeling on every form control with a hint or
            error description.</li>
          <li>Color contrast verified for every text/background pair in
            the design system against the WCAG 2.1 AA thresholds (4.5:1
            for body text, 3:1 for large text).</li>
          <li>Reduced motion respected via <code>prefers-reduced-motion</code>.</li>
          <li>Automated <code>axe-core</code> sweep on every CI build —
            serious / critical findings fail the build.</li>
        </ul>
      </section>

      <section>
        <h2>Known limitations</h2>
        <p>
          We're in the middle of a UI overhaul (Phase 6). Some legacy
          screens still use the previous card layout while the new
          design system rolls out portal-by-portal. Those screens still
          satisfy AA contrast and keyboard navigation; their visual
          density and density-mode options will improve as Story 47 of
          the active phase merges.
        </p>
      </section>

      <section>
        <h2>Report a barrier</h2>
        <p>
          If you encounter content that's hard to use, we want to know.
          Email
          <a href="mailto:accessibility&#64;quantamanalitics.com">accessibility&#64;quantamanalitics.com</a>
          with the page URL, the assistive technology you use, and a
          short description of the issue. We'll acknowledge within two
          business days and confirm a fix or a workaround within ten.
        </p>
      </section>

      <section>
        <h2>This page</h2>
        <p>
          Last updated {{ today }}. Source lives under
          <code>client/src/app/accessibility.component.ts</code>.
        </p>
      </section>
    </main>
  `,
  styles: `
    :host { display: block; }
    .page { max-width: 64ch; margin: 0 auto; padding: var(--space-6) var(--space-5); }
    .page__head { margin-bottom: var(--space-6); }
    .eyebrow {
      margin: 0 0 var(--space-2);
      color: var(--color-primary);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semi);
      text-transform: uppercase;
      letter-spacing: 0.08em;
    }
    .lede {
      color: var(--color-ink-muted);
      font-size: var(--font-size-lg);
      line-height: var(--line-height-loose);
    }
    section { padding: var(--space-5) 0; border-top: 1px solid var(--color-border); }
    section h2 { font-size: var(--font-size-xl); margin-bottom: var(--space-3); }
    section ul { padding-left: var(--space-5); }
    section li { margin-bottom: var(--space-2); }
    code { font-family: var(--font-mono); font-size: 0.9em; padding: 0 var(--space-1); background: var(--color-surface-alt); border-radius: var(--radius-sm); }
  `,
})
export class AccessibilityComponent {
  protected readonly today = new Date().toISOString().slice(0, 10);
}
