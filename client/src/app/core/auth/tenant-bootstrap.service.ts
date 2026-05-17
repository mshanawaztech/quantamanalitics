import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Client wrapper for the self-service tenant bootstrap shipped in
 * qa001-tenant-bootstrap. Surfaced by the global "join the demo tenant"
 * banner in the marketing shell when the signed-in user has no
 * tenant_id claim.
 */
@Injectable({ providedIn: 'root' })
export class TenantBootstrapService {
  private http = inject(HttpClient);
  // Absolute API base — without environment.apiBase the relative path
  // resolves against the SWA host instead of the Container App and
  // returns 405 / 404.
  private base = `${environment.apiBase}/api/v1/me/tenant`;

  /** Returns 404 when the user has no membership yet. */
  getMembership(): Observable<TenantMembership> {
    return this.http.get<TenantMembership>(`${this.base}/`);
  }

  /** Attaches the signed-in user to the seeded demo tenant. Idempotent. */
  joinDemo(): Observable<TenantMembership> {
    return this.http.post<TenantMembership>(`${this.base}/join-demo`, null);
  }
}

export interface TenantMembership {
  tenantId: string;
  slug: string;
  name: string;
  joinedAtUtc: string;
}
