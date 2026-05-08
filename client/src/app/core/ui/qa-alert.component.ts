import { Component, Input } from '@angular/core';

/**
 * Inline alert / banner. Use for non-modal feedback. The role / aria-live
 * combination tells screen readers to announce the message politely
 * (info / success) or assertively (warning / danger).
 */
@Component({
  selector: 'qa-alert',
  standalone: true,
  template: `
    <div
      [class]="'qa-alert qa-alert--' + tone"
      [attr.role]="tone === 'danger' || tone === 'warning' ? 'alert' : 'status'"
      [attr.aria-live]="tone === 'danger' || tone === 'warning' ? 'assertive' : 'polite'"
    >
      @if (title) {
        <p class="qa-alert__title">{{ title }}</p>
      }
      <div class="qa-alert__body">
        <ng-content></ng-content>
      </div>
    </div>
  `,
  styles: `
    :host { display: block; }

    .qa-alert {
      padding: var(--space-3) var(--space-4);
      border: 1px solid transparent;
      border-radius: var(--radius-md);
      font-size: var(--font-size-sm);
    }

    .qa-alert__title {
      margin: 0 0 var(--space-1);
      font-weight: var(--font-weight-semi);
    }

    .qa-alert__body :last-child { margin-bottom: 0; }

    .qa-alert--info {
      background: var(--color-info-soft);
      border-color: var(--color-info);
      color: var(--color-info);
    }
    .qa-alert--success {
      background: var(--color-success-soft);
      border-color: var(--color-success);
      color: var(--color-success);
    }
    .qa-alert--warning {
      background: var(--color-warning-soft);
      border-color: var(--color-warning);
      color: var(--color-warning);
    }
    .qa-alert--danger {
      background: var(--color-danger-soft);
      border-color: var(--color-danger);
      color: var(--color-danger);
    }
  `,
})
export class QaAlertComponent {
  @Input() tone: 'info' | 'success' | 'warning' | 'danger' = 'info';
  @Input() title?: string;
}
