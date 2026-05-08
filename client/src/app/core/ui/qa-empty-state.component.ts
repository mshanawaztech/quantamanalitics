import { Component, Input } from '@angular/core';

/**
 * Empty-state primitive for "this section has no content yet".
 * Always pair the title with a description that tells the user what
 * would appear here once the relevant action happens — empty states
 * with no context read as broken.
 */
@Component({
  selector: 'qa-empty-state',
  standalone: true,
  template: `
    <div class="qa-empty">
      @if (icon) {
        <div class="qa-empty__icon" aria-hidden="true">{{ icon }}</div>
      }
      <h4 class="qa-empty__title">{{ title }}</h4>
      @if (description) {
        <p class="qa-empty__desc">{{ description }}</p>
      }
      <ng-content></ng-content>
    </div>
  `,
  styles: `
    :host { display: block; }

    .qa-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--space-2);
      padding: var(--space-7) var(--space-5);
      text-align: center;
      background: var(--color-surface-alt);
      border: 1px dashed var(--color-border-strong);
      border-radius: var(--radius-lg);
    }

    .qa-empty__icon {
      width: 3rem; height: 3rem;
      display: grid; place-items: center;
      border-radius: var(--radius-pill);
      background: var(--color-primary-soft);
      color: var(--color-primary);
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-bold);
      margin-bottom: var(--space-2);
    }

    .qa-empty__title {
      margin: 0;
      font-size: var(--font-size-lg);
      color: var(--color-ink-strong);
    }

    .qa-empty__desc {
      max-width: 32rem;
      margin: 0;
      color: var(--color-ink-muted);
    }
  `,
})
export class QaEmptyStateComponent {
  @Input() title = 'Nothing here yet';
  @Input() description?: string;
  @Input() icon?: string;
}
