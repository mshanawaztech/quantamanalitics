import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface MeResponse {
  sub: string;
  email: string;
  name: string;
  roles: string[];
  tenantId: string | null;
}

/**
 * Calls `GET /me` on the API once Auth0 reports an authenticated session.
 * The Auth0 HTTP interceptor (registered in app.config) attaches the Bearer
 * token automatically, so this service just makes a plain HttpClient call.
 *
 * Useful sanity check that the JWT round-trip works end to end:
 *   1. Auth0 issues an access token with our API audience.
 *   2. The interceptor adds it to /me.
 *   3. The API validates the token, decodes claims, returns the payload.
 *   4. This signal flips from null → MeResponse.
 */
@Injectable({ providedIn: 'root' })
export class MeService {
  private http = inject(HttpClient);
  private auth = inject(AuthService);

  readonly loading = signal(false);
  readonly data = signal<MeResponse | null>(null);
  readonly error = signal<string | null>(null);

  readonly hasResult = computed(
    () => this.data() !== null || this.error() !== null,
  );

  constructor() {
    // Whenever the auth state flips to authenticated, fetch /me. When it
    // flips back to logged out, clear stored data.
    effect(() => {
      if (!this.auth.isAuthenticated()) {
        this.data.set(null);
        this.error.set(null);
        return;
      }

      this.loading.set(true);
      this.error.set(null);

      this.http
        .get<MeResponse>(`${environment.apiBase}/me`)
        .pipe(
          catchError((err: unknown) => {
            const message =
              err instanceof Error
                ? err.message
                : (err as { statusText?: string })?.statusText ??
                  'Unknown error calling /me';
            this.error.set(message);
            return of(null);
          }),
        )
        .subscribe((res) => {
          if (res) {
            this.data.set(res);
          }
          this.loading.set(false);
        });
    });
  }
}
