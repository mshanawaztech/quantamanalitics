import { Injectable, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AuthService as Auth0Service, User } from '@auth0/auth0-angular';

import { environment } from '../../../environments/environment';

/**
 * Thin facade over @auth0/auth0-angular. Exposes signals for template
 * binding and centralizes login/logout calls so the rest of the app never
 * imports the SDK directly. If Auth0 isn't configured (empty domain in
 * environment), `isConfigured` is false and login attempts are no-ops —
 * lets the SPA still run in pre-Auth0 environments without crashing.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private auth0 = inject(Auth0Service);

  /** True when Auth0 SDK has a domain configured. */
  readonly isConfigured = signal(!!environment.auth0.domain);

  /** True after Auth0 finishes hydrating session state. */
  readonly isAuthenticated = toSignal(this.auth0.isAuthenticated$, {
    initialValue: false,
  });

  /** Auth0's loading flag — true during silent token refresh / hydration. */
  readonly isLoading = toSignal(this.auth0.isLoading$, { initialValue: true });

  /** Current user profile (null when logged out / loading). */
  readonly user = toSignal<User | null | undefined>(this.auth0.user$, {
    initialValue: null,
  });

  /** Convenience accessor for the email claim. */
  readonly email = computed(() => this.user()?.email ?? null);

  loginWithRedirect(): void {
    if (!this.isConfigured()) {
      return;
    }
    this.auth0.loginWithRedirect();
  }

  logout(): void {
    if (!this.isConfigured()) {
      return;
    }
    this.auth0.logout({
      logoutParams: {
        // Auth0 redirects back to this URL after logout. Must be in the
        // application's "Allowed Logout URLs" in the Auth0 dashboard.
        returnTo:
          typeof window !== 'undefined' ? window.location.origin : undefined,
      },
    });
  }
}
