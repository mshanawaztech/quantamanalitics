import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';

import { environment } from '../../../environments/environment';
import { ParsedResumeResult } from '../resume/resume-parse.models';

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
  daysInStage: number;
  isStuck: boolean;
  tags: string[];
}

export interface RecruiterApplicationsBoard {
  items: RecruiterApplication[];
  availableTags: string[];
  availableLocations: string[];
  savedFilters: RecruiterApplicationFilterPreset[];
}

export interface RecruiterBulkStatusMoveRequest {
  applicationIds: string[];
  status: string;
}

export interface RecruiterBulkStatusMoveResponse {
  requestedCount: number;
  updatedCount: number;
  status: string;
  items: RecruiterApplication[];
}

export interface RecruiterBulkTagUpdateRequest {
  applicationIds: string[];
  tags: string[];
  operation: 'Add' | 'Remove';
}

export interface RecruiterBulkTagUpdateResponse {
  requestedCount: number;
  operation: string;
  tags: string[];
  items: RecruiterApplication[];
}

export interface RecruiterApplicationFilterPreset {
  id: string;
  name: string;
  search: string | null;
  status: string | null;
  tag: string | null;
  location: string | null;
  stuckOnly: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface RecruiterApplicationsQuery {
  search?: string | null;
  status?: string | null;
  tag?: string | null;
  location?: string | null;
  stuckOnly?: boolean;
}

export interface SaveRecruiterApplicationFilterPresetRequest {
  presetId?: string | null;
  name: string;
  search?: string | null;
  status?: string | null;
  tag?: string | null;
  location?: string | null;
  stuckOnly: boolean;
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

export interface EmailTemplatePreset {
  slug: string;
  name: string;
  description: string;
  subject: string;
  bodyMarkdown: string;
  mergeFields: string[];
}

export interface EmailTemplateCatalog {
  presets: EmailTemplatePreset[];
  supportedMergeFields: string[];
}

export interface RecruiterEmailTemplate {
  id: string;
  slug: string;
  name: string;
  subject: string;
  bodyMarkdown: string;
  createdByAuthSubject: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface RecruiterEmailTemplateList {
  items: RecruiterEmailTemplate[];
}

export interface CreateRecruiterEmailTemplateRequest {
  slug: string;
  name: string;
  subject: string;
  bodyMarkdown: string;
}

export interface UpdateRecruiterEmailTemplateRequest {
  name?: string | null;
  subject?: string | null;
  bodyMarkdown?: string | null;
}

export interface EmailTemplatePreviewRequest {
  subject: string;
  bodyMarkdown: string;
  mergeFields: Record<string, string | null>;
}

export interface EmailTemplatePreviewResponse {
  subject: string;
  bodyMarkdown: string;
  mergeFields: Record<string, string>;
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

  applications(query?: RecruiterApplicationsQuery) {
    let params = new HttpParams();

    if (query?.search?.trim()) {
      params = params.set('search', query.search.trim());
    }

    if (query?.status?.trim()) {
      params = params.set('status', query.status.trim());
    }

    if (query?.tag?.trim()) {
      params = params.set('tag', query.tag.trim());
    }

    if (query?.location?.trim()) {
      params = params.set('location', query.location.trim());
    }

    if (query?.stuckOnly) {
      params = params.set('stuckOnly', 'true');
    }

    return this.http.get<RecruiterApplicationsBoard>(
      `${environment.apiBase}/api/v1/recruiter/applications`,
      { params },
    );
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

  bulkMoveApplications(request: RecruiterBulkStatusMoveRequest) {
    return this.http.post<RecruiterBulkStatusMoveResponse>(
      `${environment.apiBase}/api/v1/recruiter/applications/bulk-status`,
      request,
    );
  }

  bulkUpdateApplicationTags(request: RecruiterBulkTagUpdateRequest) {
    return this.http.post<RecruiterBulkTagUpdateResponse>(
      `${environment.apiBase}/api/v1/recruiter/applications/bulk-tags`,
      request,
    );
  }

  saveApplicationFilterPreset(request: SaveRecruiterApplicationFilterPresetRequest) {
    return this.http.post<RecruiterApplicationFilterPreset>(
      `${environment.apiBase}/api/v1/recruiter/applications/filters`,
      request,
    );
  }

  deleteApplicationFilterPreset(presetId: string) {
    return this.http.delete(
      `${environment.apiBase}/api/v1/recruiter/applications/filters/${presetId}`,
    );
  }

  parseResume(file: File) {
    const body = new FormData();
    body.append('file', file);

    return this.http.post<ParsedResumeResult>(
      `${environment.apiBase}/api/v1/recruiter/resume/parse`,
      body,
    );
  }

  emailTemplateCatalog() {
    return this.http.get<EmailTemplateCatalog>(
      `${environment.apiBase}/api/v1/recruiter/email-templates/catalog`,
    );
  }

  emailTemplates() {
    return this.http.get<RecruiterEmailTemplateList>(
      `${environment.apiBase}/api/v1/recruiter/email-templates`,
    );
  }

  createEmailTemplate(request: CreateRecruiterEmailTemplateRequest) {
    return this.http.post<RecruiterEmailTemplate>(
      `${environment.apiBase}/api/v1/recruiter/email-templates`,
      request,
    );
  }

  updateEmailTemplate(templateId: string, request: UpdateRecruiterEmailTemplateRequest) {
    return this.http.patch<RecruiterEmailTemplate>(
      `${environment.apiBase}/api/v1/recruiter/email-templates/${templateId}`,
      request,
    );
  }

  deleteEmailTemplate(templateId: string) {
    return this.http.delete(
      `${environment.apiBase}/api/v1/recruiter/email-templates/${templateId}`,
    );
  }

  previewEmailTemplate(request: EmailTemplatePreviewRequest) {
    return this.http.post<EmailTemplatePreviewResponse>(
      `${environment.apiBase}/api/v1/recruiter/email-templates/preview`,
      request,
    );
  }
}
