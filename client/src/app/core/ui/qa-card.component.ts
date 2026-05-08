import { Component, Input } from '@angular/core';

/**
 * Surface primitive for grouped content. Header / body / footer slots.
 * Optional `title` and `subtitle` inputs short-circuit the header slot.
 */
@Component({
  selector: 'qa-card',
  standalone: true,
  template: `
    <article [class]="'qa-card ' + (interactive ? 'qa-card--interactive' : '')">
      @if (title || subtitle) {
        <header class="qa-card__header">
          @if (eyebrow) {
            <p class="qa-card__eyebrow">{{ eyebrow }}</p>
          }
          @if (title) {
            <h3 class="qa-card__title">{{ title }}</h3>
          }
          @if (subtitle) {
            <p class="qa-card__subtitle">{{ subtitle }}</p>
          }
        </header>
      } @else {
        <ng-content select="[card-header]"></ng-content>
      }

      <div class="qa-card__body">
        <ng-content></ng-content>
      </div>

      <ng-content select="[card-footer]"></ng-content>
    </article>
  `,
  styles: `
    :host { display: block; }

    .qa-card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm);
      overflow: hidden;
    }

    .qa-card--interactive {
      cursor: pointer;
      transition: border-color 160ms ease, box-shadow 160ms ease,
                  transform 160ms ease;
    }
    .qa-card--interactive:hover {
      border-color: var(--color-primary);
      box-shadow: var(--shadow-md);
      transform: translateY(-1px);
    }

    .qa-card__header {
      padding: var(--space-4) var(--space-5) var(--space-3);
      border-bottom: 1px solid var(--color-border);
    }

    .qa-card__eyebrow {
      margin: 0 0 var(--space-1);
      color: var(--color-primary);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semi);
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }

    .qa-card__title {
      margin: 0;
      font-size: var(--font-size-lg);
      color: var(--color-ink-strong);
    }

    .qa-card__subtitle {
      margin: var(--space-1) 0 0;
      color: var(--color-ink-muted);
      font-size: var(--font-size-sm);
    }

    .qa-card__body {
      padding: var(--space-5);
    }

    .qa-card__body > :last-child { margin-bottom: 0; }
  `,
})
export class QaCardComponent {
  @Input() title?: string;
  @Input() subtitle?: string;
  @Input() eyebrow?: string;
  @Input() interactive = false;
}
