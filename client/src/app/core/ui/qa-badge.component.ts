import { Component, Input } from '@angular/core';

/**
 * Status pill. Color is tied to semantic intent (not used purely as
 * decoration), so screen readers announce the role + the text.
 */
@Component({
  selector: 'qa-badge',
  standalone: true,
  template: `
    <span [class]="'qa-badge qa-badge--' + tone" [attr.aria-label]="ariaLabel || null">
      <ng-content></ng-content>
    </span>
  `,
  styles: `
    :host { display: inline-block; }

    .qa-badge {
      display: inline-flex;
      align-items: center;
      gap: var(--space-1);
      padding: 2px var(--space-2);
      min-height: 1.5rem;
      border-radius: var(--radius-pill);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semi);
      letter-spacing: 0.02em;
      line-height: 1;
    }

    .qa-badge--neutral { background: var(--color-surface-alt); color: var(--color-ink-muted); }
    .qa-badge--info    { background: var(--color-info-soft); color: var(--color-info); }
    .qa-badge--primary { background: var(--color-primary-soft); color: var(--color-primary); }
    .qa-badge--success { background: var(--color-success-soft); color: var(--color-success); }
    .qa-badge--warning { background: var(--color-warning-soft); color: var(--color-warning); }
    .qa-badge--danger  { background: var(--color-danger-soft); color: var(--color-danger); }
  `,
})
export class QaBadgeComponent {
  @Input() tone: 'neutral' | 'info' | 'primary' | 'success' | 'warning' | 'danger' = 'neutral';
  @Input() ariaLabel?: string;
}
