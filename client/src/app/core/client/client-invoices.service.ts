import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  InvoiceListResponse,
  InvoiceResponse,
} from '../contractor/invoices.service';

/**
 * Client-side review of contractor invoices. The contractor submits a draft
 * (Draft → Submitted); a client approver acts on it here:
 *   Submitted → Approved | Rejected, then Approved → Paid (payroll/billing).
 *
 * Endpoints:
 *   GET  /api/v1/client/invoices            list submitted/approved/rejected/paid
 *   POST /api/v1/client/invoices/{id}/approve   { note? }
 *   POST /api/v1/client/invoices/{id}/reject    { note }  (note required)
 *   POST /api/v1/recruiter/invoices/{id}/mark-paid        (payroll access)
 */
@Injectable({ providedIn: 'root' })
export class ClientInvoicesService {
  private http = inject(HttpClient);
  private clientBase = `${environment.apiBase}/api/v1/client/invoices`;
  private payrollBase = `${environment.apiBase}/api/v1/recruiter/invoices`;

  list(): Observable<InvoiceListResponse> {
    return this.http.get<InvoiceListResponse>(this.clientBase);
  }

  approve(id: string, note: string | null): Observable<InvoiceResponse> {
    return this.http.post<InvoiceResponse>(`${this.clientBase}/${id}/approve`, { note });
  }

  reject(id: string, note: string): Observable<InvoiceResponse> {
    return this.http.post<InvoiceResponse>(`${this.clientBase}/${id}/reject`, { note });
  }

  markPaid(id: string): Observable<InvoiceResponse> {
    return this.http.post<InvoiceResponse>(`${this.payrollBase}/${id}/mark-paid`, null);
  }
}
