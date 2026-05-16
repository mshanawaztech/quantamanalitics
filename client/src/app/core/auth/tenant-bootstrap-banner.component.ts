import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { AuthService } from './auth.service';
import { MeService } from './me.service';
import { TenantBootstrapService } from './tenant-bootstrap.service';

/**
 * Renders at the top of the authenticated shell when the signed-in user
 * has no tenant_id claim. Without this banner the user lands on every
 * portal and sees a 412 empty state ("Authenticated without tenant
 * context") with no in-app recovery.
 *
 * Posting to /api/v1/me/tenant/join-demo attaches the user to the
 * seeded demo tenant. The middleware now reads tenant from the
 * memberships table when the JWT doesn't carry the claim, so the next
 * request is tenant-scoped without a re-sign-in.
 */
@Component({
  selector: 'app-tenant-bootstrap-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible()) {
      <div class="banner" role="status" aria-live="polite">
        <div class="banner__copy">
          <strong>You're signed in, but your account isn't attached to a tenant yet.</strong>
          <span>
            Every portal is locked behind a tenant. Click below to join the
            seeded demo tenant — instant, idempotent, reversible later.
          </span>
        </div>
        <div class="banner__actions">
          @if (status() === 'success') {
            <span class="banner__success">Joined — refresh the page.</span>
            <button type="button" class="banner__btn banner__btn--primary" (click)="refresh()">
              Refresh now
            </button>
          } @else {
            <button
              type="button"
              class="banner__btn banner__btn--primary"
              [disabled]="status() === 'joining'"
              (click)="join()"
            >{{ status() === 'joining' ? 'Joining…' : 'Join demo tenant' }}</button>
          }
        </div>
        @if (error()) {
          <p class="banner__error" role="alert">{{ error() }}</p>
        }
      </div>
    }
  `,
  styles: `
    :host { display: block; }
    .banner {
      background: #fef3c7;
      color: #78350f;
      border: 1px solid #fcd34d;
      border-radius: 12px;
      padding: 0.875rem 1rem;
      margin: 1rem 1.5rem 0;
      display: flex;
      align-items: center;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .banner__copy { display: flex; flex-direction: column; gap: 2px; flex: 1; min-width: 280px; }
    .banner__copy strong { font-size: 0.95rem; }
    .banner__copy span { font-size: 0.85rem; color: #92400e; line-height: 1.4; }
    .banner__actions { display: flex; gap: 0.5rem; align-items: center; }
    .banner__btn {
      padding: 0.5rem 0.875rem;
      border: 0;
      border-radius: 8px;
      font-weight: 600;
      cursor: pointer;
      font-size: 0.9rem;
    }
    .banner__btn--primary {
      background: var(--color-primary, #1a3a8f);
      color: #fff;
    }
    .banner__btn--primary:hover { background: var(--color-primary-hover, #0d2766); }
    .banner__btn:disabled { opacity: 0.6; cursor: wait; }
    .banner__success { font-size: 0.85rem; font-weight: 600; color: #14532d; }
    .banner__error {
      margin: 0;
      width: 100%;
      font-size: 0.85rem;
      color: #991b1b;
    }
  `,
})
export class TenantBootstrapBannerComponent {
  protected readonly auth = inject(AuthService);
  protected readonly me = inject(MeService);
  private readonly bootstrap = inject(TenantBootstrapService);

  protected readonly status = signal<'idle' | 'joining' | 'success'>('idle');
  protected readonly error = signal<string | null>(null);

  // Membership state. Null until /me/tenant is checked, false when 404.
  private readonly membership = signal<boolean | null>(null);

  /**
   * Banner is visible only when: user is authenticated, the /me response
   * carries no tenant id, AND the membership lookup confirmed 404 (so
   * we don't flash the banner on first load before knowing).
   */
  protected readonly visible = computed(() => {
    if (!this.auth.isAuthenticated()) return false;
    const me = this.me.data();
    if (me?.tenantId) return false;
    return this.membership() === false || this.status() === 'success';
  });

  constructor() {
    // One-shot membership probe on construction; the auth signal change
    // re-fires the visibility computed when /me populates.
    this.bootstrap.getMembership().subscribe({
      next: () => this.membership.set(true),
      error: (err: unknown) => {
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this.membership.set(false);
        } else if (err instanceof HttpErrorResponse && err.status === 401) {
          // Not signed in — banner stays hidden by the auth check.
          this.membership.set(null);
        } else {
          this.membership.set(false);
        }
      },
    });
  }

  protected join(): void {
    this.status.set('joining');
    this.error.set(null);

    this.bootstrap.joinDemo().subscribe({
      next: () => {
        this.status.set('success');
        this.membership.set(true);
      },
      error: (err: unknown) => {
        this.status.set('idle');
        this.error.set(
          err instanceof HttpErrorResponse
            ? (err.error?.detail ?? err.message)
            : 'Unable to join the demo tenant.',
        );
      },
    });
  }

  protected refresh(): void {
    window.location.reload();
  }
}
