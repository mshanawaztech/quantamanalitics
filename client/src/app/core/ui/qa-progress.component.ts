import { Component, Input } from '@angular/core';

/**
 * Linear progress bar. Use for onboarding completion %, burn-rate fills,
 * candidate scoring. Cobalt gradient on a soft track. Announces percentage
 * to assistive tech via aria-valuenow.
 */
@Component({
  selector: 'qa-progress',
  standalone: true,
  template: `
    <div
      class="bar"
      role="progressbar"
      [attr.aria-valuenow]="clamped()"
      aria-valuemin="0"
      aria-valuemax="100"
      [attr.aria-label]="ariaLabel || clamped() + '% complete'"
    >
      <i [style.width.%]="clamped()"></i>
    </div>
  `,
  styles: `
    :host { display: block; min-width: 7.5rem; }
    .bar {
      height: 8px;
      border-radius: 999px;
      background: var(--color-surface-alt, #f0f3f9);
      overflow: hidden;
    }
    .bar > i {
      display: block;
      height: 100%;
      background: linear-gradient(90deg, #1e5fd0, var(--color-primary, #1a3a8f));
      transition: width 240ms ease;
    }
  `,
})
export class QaProgressComponent {
  @Input({ required: true }) value!: number;
  @Input() ariaLabel?: string;

  clamped(): number {
    if (!Number.isFinite(this.value)) return 0;
    return Math.max(0, Math.min(100, Math.round(this.value)));
  }
}
