import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Wrapper for the contractor-side invoice endpoints shipped in Phase 6
 * Story 48. Endpoints under /api/v1/contractor/invoices:
 *   GET  /            list-mine — recipient-filtered server-side
 *   POST /            create draft
 *   POST /{id}/submit transition Draft → Submitted (locks editing)
 */
@Injectable({ providedIn: 'root' })
export class ContractorInvoicesService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/api/v1/contractor/invoices`;

  list(): Observable<InvoiceListResponse> {
    return this.http.get<InvoiceListResponse>(this.base);
  }

  create(request: CreateInvoiceRequest): Observable<InvoiceResponse> {
    return this.http.post<InvoiceResponse>(this.base, request);
  }

  submit(id: string): Observable<InvoiceResponse> {
    return this.http.post<InvoiceResponse>(`${this.base}/${id}/submit`, null);
  }
}

export interface CreateInvoiceRequest {
  periodStartUtc: string;
  periodEndUtc: string;
  hours: number;
  amount: number;
  currency: string;
  notes: string | null;
}

export interface InvoiceListResponse {
  items: InvoiceResponse[];
}

export interface InvoiceResponse {
  id: string;
  contractorEmail: string;
  periodStartUtc: string;
  periodEndUtc: string;
  hours: number;
  amount: number;
  currency: string;
  notes: string | null;
  status: string;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  reviewerNote: string | null;
  paidAtUtc: string | null;
  updatedAtUtc: string;
}
