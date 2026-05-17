import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Per-tenant branding profile. The backend GET auto-creates a row on first
 * read so the SPA always gets back a populated response — it can then PUT
 * the whole thing back as the contractor edits the form.
 */
@Injectable({ providedIn: 'root' })
export class TenantBrandingService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/api/v1/tenant/branding`;

  get(): Observable<TenantBrandingResponse> {
    return this.http.get<TenantBrandingResponse>(this.base);
  }

  update(request: UpdateTenantBrandingRequest): Observable<TenantBrandingResponse> {
    return this.http.put<TenantBrandingResponse>(this.base, request);
  }
}

export interface TenantBrandingResponse {
  displayName: string | null;
  legalName: string | null;
  contactEmail: string | null;
  contactPhone: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  stateRegion: string | null;
  postalCode: string | null;
  country: string | null;
  bankName: string | null;
  bankAccountNumber: string | null;
  bankRoutingNumber: string | null;
  defaultHourlyRate: number | null;
  defaultCurrency: string | null;
  defaultPaymentTermsDays: number | null;
  primaryColorHex: string | null;
  accentColorHex: string | null;
  hasLogo: boolean;
  updatedAtUtc: string;
}

export type UpdateTenantBrandingRequest = Omit<TenantBrandingResponse, 'hasLogo' | 'updatedAtUtc'>;
