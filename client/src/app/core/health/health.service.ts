import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, finalize, of } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface HealthResponse {
  status: string;
  service: string;
  version: string;
  timestamp: string;
}

/**
 * Probes the API's /health endpoint on construction. Used by the scaffold
 * landing page to verify the front-end can reach the back-end. Replaced
 * later by real auth-aware bootstrap calls.
 */
@Injectable({ providedIn: 'root' })
export class HealthService {
  private http = inject(HttpClient);

  readonly loading = signal(true);
  readonly data = signal<HealthResponse | null>(null);
  readonly error = signal<string | null>(null);

  constructor() {
    this.http
      .get<HealthResponse>(`${environment.apiBase}/health`)
      .pipe(
        catchError((err) => {
          this.error.set(err?.statusText || err?.message || 'unknown error');
          return of(null);
        }),
        finalize(() => this.loading.set(false)),
      )
      .subscribe((res) => {
        if (res) this.data.set(res);
      });
  }
}
