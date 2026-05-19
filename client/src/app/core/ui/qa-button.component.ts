import { Component, Input } from '@angular/core';

/**
 * Accessible button primitive. Use instead of raw <button>.
 *
 * Variants:
 *   - primary       — main call to action (deep blue fill)
 *   - secondary     — secondary action (white fill, blue border)
 *   - ghost         — least emphasis (no fill, no border)
 *   - destructive   — irreversible action (red fill)
 *
 * Always renders a real <button>. Set `type="submit"` when used inside
 * forms. Visible focus ring meets WCAG 2.4.7.
 */
@Component({
  selector: 'qa-button',
  standalone: true,
  template: `
    <button
      [attr.type]="type"
      [attr.aria-label]="ariaLabel || null"
      [attr.aria-busy]="loading || null"
      [disabled]="disabled"
      [class]="'qa-btn qa-btn--' + variant + (block ? ' qa-btn--block' : '')"
    >
      <ng-content></ng-content>
    </button>
  `,
  styles: `
    :host { display: inline-block; }

    .qa-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: var(--space-2);
      padding: var(--space-2) var(--space-4);
      min-height: 2.5rem;
      border: 1px solid transparent;
      border-radius: var(--radius-md);
      font: inherit;
      font-weight: var(--font-weight-semi);
      font-size: var(--font-size-sm);
      letter-spacing: 0.01em;
      cursor: pointer;
      transition: background-color 160ms ease, border-color 160ms ease,
                  color 160ms ease, transform 160ms ease;
    }

    .qa-btn:focus-visible {
      outline: 3px solid var(--color-primary-ring);
      outline-offset: 2px;
    }

    .qa-btn:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }

    .qa-btn:active:not(:disabled) { transform: translateY(1px); }

    .qa-btn--block { width: 100%; }

    /* Primary — deep-blue fill */
    .qa-btn--primary {
      background: var(--color-primary);
      color: var(--color-ink-onblue);
      border-color: var(--color-primary);
    }
    .qa-btn--primary:hover:not(:disabled) {
      background: var(--color-primary-hover);
      border-color: var(--color-primary-hover);
    }

    /* Secondary — outlined */
    .qa-btn--secondary {
      background: var(--color-surface);
      color: var(--color-primary);
      border-color: var(--color-primary);
    }
    .qa-btn--secondary:hover:not(:disabled) {
      background: var(--color-primary-soft);
    }

    /* Ghost — minimal */
    .qa-btn--ghost {
      background: transparent;
      color: var(--color-primary);
      border-color: transparent;
    }
    .qa-btn--ghost:hover:not(:disabled) {
      background: var(--color-primary-soft);
    }

    /* Destructive — red fill, used for irreversible actions */
    .qa-btn--destructive {
      background: var(--color-danger);
      color: var(--color-ink-onblue);
      border-color: var(--color-danger);
    }
    .qa-btn--destructive:hover:not(:disabled) { opacity: 0.9; }
  `,
})
export class QaButtonComponent {
  @Input() variant: 'primary' | 'secondary' | 'ghost' | 'destructive' = 'primary';
  @Input() type: 'button' | 'submit' | 'reset' = 'button';
  @Input() disabled = false;
  @Input() block = false;
  @Input() ariaLabel?: string;
  /**
   * When true, sets `aria-busy="true"` so screen readers announce the
   * "working" state. Doesn't itself disable the button — wire `disabled`
   * alongside if you want to block additional clicks during the operation.
   */
  @Input() loading = false;
}
