import { Component, Input } from '@angular/core';

/**
 * Page header primitive: optional eyebrow, h1 title, optional lede, and an
 * action slot on the right. Used at the top of every portal screen so the
 * vertical rhythm and typography stay consistent.
 */
@Component({
  selector: 'qa-page-head',
  standalone: true,
  template: `
    <header class="head">
      <div class="head__copy">
        @if (eyebrow) {
          <p class="eyebrow">{{ eyebrow }}</p>
        }
        <h1>{{ title }}</h1>
        @if (lede) {
          <p class="lede">{{ lede }}</p>
        }
      </div>
      <div class="head__actions">
        <ng-content></ng-content>
      </div>
    </header>
  `,
  styles: `
    :host {
      display: block;
      margin-bottom: 1.5rem;
    }
    .head {
      display: flex;
      justify-content: space-between;
      align-items: flex-end;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .eyebrow {
      margin: 0 0 0.35rem;
      color: var(--color-primary, #1a3a8f);
      text-transform: uppercase;
      letter-spacing: 0.06em;
      font-size: 0.74rem;
      font-weight: 800;
    }
    h1 {
      margin: 0;
      font-size: clamp(1.65rem, 2.6vw, 2rem);
      color: var(--color-ink-strong, #0d1b2a);
      line-height: 1.1;
      font-weight: 800;
    }
    .lede {
      margin: 0.4rem 0 0;
      color: var(--color-ink-muted, #4b5a72);
      max-width: 60ch;
    }
    .head__actions {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    @media (max-width: 720px) {
      .head { flex-direction: column; align-items: flex-start; }
    }
  `,
})
export class QaPageHeadComponent {
  @Input({ required: true }) title!: string;
  @Input() eyebrow?: string;
  @Input() lede?: string;
}
