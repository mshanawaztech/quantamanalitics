import { Component, Input } from '@angular/core';

/**
 * Stat card primitive: small uppercase label, large value, optional detail
 * (with a "trend up" flag that colors it success-green). Self-contained card
 * surface — drop straight into a CSS grid for a KPI row.
 */
@Component({
  selector: 'qa-stat',
  standalone: true,
  template: `
    <span class="k">{{ label }}</span>
    <span class="v">{{ value }}</span>
    @if (detail) {
      <span class="d" [class.d--up]="trendUp">{{ detail }}</span>
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 0.25rem;
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
      border-radius: var(--radius-lg, 0.75rem);
      box-shadow: var(--shadow-md, 0 4px 6px rgba(13, 27, 42, 0.06), 0 10px 18px rgba(13, 27, 42, 0.06));
      padding: 1.4rem;
    }
    .k {
      font-size: 0.74rem;
      font-weight: 800;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--color-primary, #1a3a8f);
    }
    .v {
      font-size: 1.9rem;
      font-weight: 800;
      color: var(--color-ink-strong, #0d1b2a);
      line-height: 1;
    }
    .d {
      font-size: 0.85rem;
      color: var(--color-ink-muted, #4b5a72);
    }
    .d--up {
      color: var(--color-success, #146c43);
      font-weight: 700;
    }
  `,
})
export class QaStatComponent {
  @Input({ required: true }) label!: string;
  @Input({ required: true }) value!: string | number;
  @Input() detail?: string;
  @Input() trendUp = false;
}
