import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface RecruiterJob {
  id: string;
  title: string;
  slug: string;
  location: string;
  summary: string;
  description: string;
  postedOnUtc: string;
  isPublished: boolean;
}

export interface RecruiterApplication {
  id: string;
  jobId: string;
  jobTitle: string;
  candidateName: string;
  candidateEmail: string;
  note: string | null;
  status: string;
  appliedAtUtc: string;
  updatedAtUtc: string;
}

export interface RecruiterApplicationsBoard {
  items: RecruiterApplication[];
}

export interface RecruiterCandidateActivityItem {
  id: string;
  candidateName: string;
  candidateEmail: string;
  applicationId: string | null;
  jobId: string | null;
  jobTitle: string | null;
  jobSlug: string | null;
  eventType: string;
  title: string;
  detail: string | null;
  actorLabel: string | null;
  status: string | null;
  occurredAtUtc: string;
}

export interface RecruiterCandidateActivityResponse {
  items: RecruiterCandidateActivityItem[];
}

export interface RecruiterInvoiceReadyItem {
  timesheetId: string;
  contractorEmail: string;
  weekStartUtc: string;
  approvedAtUtc: string | null;
  regularHours: number;
  overtimeHours: number;
  paidTimeOffHours: number;
  payableHours: number;
}

export interface RecruiterInvoiceReadyResponse {
  items: RecruiterInvoiceReadyItem[];
}

export interface RecruiterStripeFallbackItem {
  timesheetId: string;
  contractorEmail: string;
  description: string;
  quantity: number;
  unit: string;
  collectionMethod: string;
}

export interface RecruiterStripeFallbackBatch {
  batchReference: string;
  items: RecruiterStripeFallbackItem[];
}

export interface RecruiterInvoiceHandoffResponse {
  quickBooksFileName: string;
  approvedTimesheetCount: number;
  totalPayableHours: number;
  stripeFallback: RecruiterStripeFallbackBatch;
}

export interface UpsertRecruiterJobRequest {
  title: string;
  location: string;
  summary: string;
  description: string;
  isPublished: boolean;
  postedOnUtc: string | null;
}

@Injectable({ providedIn: 'root' })
export class RecruiterPortalService {
  private http = inject(HttpClient);

  jobs() {
    return this.http.get<RecruiterJob[]>(`${environment.apiBase}/api/v1/recruiter/jobs`);
  }

  createJob(request: UpsertRecruiterJobRequest) {
    return this.http.post<RecruiterJob>(`${environment.apiBase}/api/v1/recruiter/jobs`, request);
  }

  applications() {
    return this.http.get<RecruiterApplicationsBoard>(`${environment.apiBase}/api/v1/recruiter/applications`);
  }

  candidateActivity() {
    return this.http.get<RecruiterCandidateActivityResponse>(
      `${environment.apiBase}/api/v1/recruiter/candidates/activity`,
    );
  }

  invoiceReady() {
    return this.http.get<RecruiterInvoiceReadyResponse>(
      `${environment.apiBase}/api/v1/recruiter/invoice-ready`,
    );
  }

  invoiceHandoff() {
    return this.http.get<RecruiterInvoiceHandoffResponse>(
      `${environment.apiBase}/api/v1/recruiter/invoice-handoff`,
    );
  }

  quickBooksCsv() {
    return this.http.get(
      `${environment.apiBase}/api/v1/recruiter/invoice-handoff/quickbooks.csv`,
      { observe: 'response', responseType: 'blob' },
    );
  }

  moveApplication(applicationId: string, status: string) {
    return this.http.post<RecruiterApplication>(
      `${environment.apiBase}/api/v1/recruiter/applications/${applicationId}/status`,
      { status },
    );
  }
}
