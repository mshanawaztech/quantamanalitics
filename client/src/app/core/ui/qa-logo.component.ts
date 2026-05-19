import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

/**
 * Quantam Analytics brand mark.
 *
 * A bold "Q" rendered as a navy ring around a centered accent-color
 * upward chart tick. Same visual identity used in:
 *   - the global app shell header (marketing-shell)
 *   - the home page hero banner
 *   - the invoice letterhead preview (in-app + QuestPDF rendered PDF)
 *
 * Sized in `em` so it scales with the surrounding text. Override the
 * `size` input for explicit dimensions. The SVG is self-contained —
 * no external font, no rasterized asset, looks correct at any scale.
 *
 * Color tokens:
 *   --color-primary       — the Q ring
 *   --color-accent        — the chart tick inside (warm beige by default)
 * Both fall back to inline hex values so the mark renders consistently
 * even before any theme tokens load.
 */
@Component({
  selector: 'qa-logo',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      [attr.width]="size"
      [attr.height]="size"
      viewBox="0 0 64 64"
      xmlns="http://www.w3.org/2000/svg"
      role="img"
      [attr.aria-label]="alt"
    >
      <!-- Outer navy ring (the Q's circle) -->
      <circle
        cx="32"
        cy="32"
        r="26"
        fill="none"
        stroke="var(--color-primary, #1a3a8f)"
        stroke-width="6"
      />
      <!-- Q tail — short diagonal stroke at the bottom-right of the ring -->
      <line
        x1="48"
        y1="48"
        x2="58"
        y2="58"
        stroke="var(--color-primary, #1a3a8f)"
        stroke-width="6"
        stroke-linecap="round"
      />
      <!-- Inset analytics tick — three-point upward line chart inside the Q,
           colored with the accent so it pops against the ring. -->
      <polyline
        points="18,40 27,32 35,36 46,22"
        fill="none"
        stroke="var(--color-accent, #e6c9a8)"
        stroke-width="4"
        stroke-linecap="round"
        stroke-linejoin="round"
      />
      <!-- Endpoint marker on the tick's apex — emphasizes the "growth" beat. -->
      <circle cx="46" cy="22" r="3" fill="var(--color-accent, #e6c9a8)" />
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      line-height: 0;
    }
  `,
})
export class QaLogoComponent {
  /** SVG width and height in CSS pixels. Defaults to inherit (1em). */
  @Input() size: string | number = '1em';

  /** Accessible alternative text. Override when the mark stands alone. */
  @Input() alt = 'Quantam Analytics';
}
