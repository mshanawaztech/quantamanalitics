import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

/**
 * Quantam Analytics brand mark.
 *
 * A black hexagon badge holding a white "Q" (magnifier-style ring) with a
 * dark-gold bar/handle. Fixed colors — the mark reads as a solid badge on
 * white pages and pops on the cobalt header alike. Used in:
 *   - the global app shell header (marketing-shell)
 *   - the home page hero banner
 *   - the invoice letterhead preview
 *
 * Sized in `em` so it scales with surrounding text; override `size` for
 * explicit dimensions. Self-contained SVG — no external font or raster
 * asset, crisp from favicon to hero size.
 */
@Component({
  selector: 'qa-logo',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      [attr.width]="size"
      [attr.height]="size"
      viewBox="0 0 120 120"
      xmlns="http://www.w3.org/2000/svg"
      role="img"
      [attr.aria-label]="alt"
    >
      <defs>
        <linearGradient id="qaGoldShine" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stop-color="#FCEFAE"/>
          <stop offset="40%" stop-color="#F4CE54"/>
          <stop offset="72%" stop-color="#D9A91E"/>
          <stop offset="100%" stop-color="#A9791B"/>
        </linearGradient>
        <radialGradient id="qaLensBlue" cx="42%" cy="38%" r="70%">
          <stop offset="0%" stop-color="#3f86e6"/>
          <stop offset="60%" stop-color="#1e5fd0"/>
          <stop offset="100%" stop-color="#103a96"/>
        </radialGradient>
      </defs>

      <!-- Black rounded hexagon, centered at (60,60) -->
      <path
        d="M34 15 L86 15 L112 60 L86 105 L34 105 L8 60 Z"
        fill="#111111"
        stroke="#111111"
        stroke-width="7"
        stroke-linejoin="round"
      />
      <!-- Shiny gold handle (drawn before the ring so the ring overlaps it cleanly) -->
      <line x1="80" y1="81" x2="91" y2="92" stroke="url(#qaGoldShine)" stroke-width="12" stroke-linecap="round"/>
      <!-- White magnifier ring + cobalt lens, centered on the hexagon middle -->
      <circle cx="58" cy="59" r="31" fill="#ffffff"/>
      <circle cx="58" cy="59" r="25" fill="url(#qaLensBlue)"/>
      <!-- White analytics bars + gold trend line inside the lens -->
      <rect x="45" y="65" width="8" height="12" rx="2" fill="#ffffff"/>
      <rect x="56" y="59" width="8" height="18" rx="2" fill="#ffffff"/>
      <rect x="67" y="53" width="8" height="24" rx="2" fill="#ffffff"/>
      <polyline points="43,57 53,51 63,54 73,45" fill="none" stroke="#FCEFAE" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/>
      <circle cx="43" cy="57" r="3" fill="#F4CE54"/>
      <circle cx="53" cy="51" r="3" fill="#F4CE54"/>
      <circle cx="63" cy="54" r="3" fill="#F4CE54"/>
      <circle cx="73" cy="45" r="3" fill="#F4CE54"/>
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
