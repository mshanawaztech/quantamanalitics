import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Wrapper for the contractor-side invoice endpoints. v2 adds line items,
 * issue/due dates, a tax line, and auto-minted invoice numbers. The state
 * machine (Draft → Submitted → Approved/Rejected → Paid) is unchanged.
 *
 * Endpoints under /api/v1/contractor/invoices:
 *   GET  /            list-mine — recipient-filtered server-side
 *   POST /            create draft (server mints invoice number)
 *   PUT  /{id}        update a draft (line items, dates, tax, client)
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

  update(id: string, request: UpdateInvoiceRequest): Observable<InvoiceResponse> {
    return this.http.put<InvoiceResponse>(`${this.base}/${id}`, request);
  }

  submit(id: string): Observable<InvoiceResponse> {
    return this.http.post<InvoiceResponse>(`${this.base}/${id}/submit`, null);
  }

  /**
   * Fetches the rendered PDF as a Blob so the caller can trigger a browser
   * download via createObjectURL + an anchor click. Returning a Blob (not
   * a download side-effect) keeps the service testable and lets callers
   * choose between download / inline preview.
   */
  downloadPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/pdf`, { responseType: 'blob' });
  }
}

export interface InvoiceLineItemRequest {
  description: string;
  hours: number;
  rate: number;
}

export interface CreateInvoiceRequest {
  clientName: string | null;
  issueDateUtc: string;
  dueDateUtc: string;
  periodStartUtc: string;
  periodEndUtc: string;
  currency: string;
  taxRate: number;
  lineItems: InvoiceLineItemRequest[];
  notes: string | null;
}

export type UpdateInvoiceRequest = CreateInvoiceRequest;

export interface InvoiceLineItemResponse {
  id: string;
  description: string;
  hours: number;
  rate: number;
  amount: number;
  sortOrder: number;
}

export interface InvoiceListResponse {
  items: InvoiceResponse[];
}

export type InvoiceStatus =
  | 'Draft'
  | 'Submitted'
  | 'Approved'
  | 'Rejected'
  | 'Paid';

export interface InvoiceResponse {
  id: string;
  invoiceNumber: string;
  contractorEmail: string;
  clientName: string;
  issueDateUtc: string;
  dueDateUtc: string;
  periodStartUtc: string;
  periodEndUtc: string;
  hours: number;
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  amount: number;
  currency: string;
  notes: string | null;
  status: InvoiceStatus;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  reviewerNote: string | null;
  paidAtUtc: string | null;
  updatedAtUtc: string;
  lineItems: InvoiceLineItemResponse[];
}
