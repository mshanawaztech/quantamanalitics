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

  invoiceReady() {
    return this.http.get<RecruiterInvoiceReadyResponse>(
      `${environment.apiBase}/api/v1/recruiter/invoice-ready`,
    );
  }

  moveApplication(applicationId: string, status: string) {
    return this.http.post<RecruiterApplication>(
      `${environment.apiBase}/api/v1/recruiter/applications/${applicationId}/status`,
      { status },
    );
  }
}
