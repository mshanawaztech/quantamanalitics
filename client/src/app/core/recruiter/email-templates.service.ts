import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * Thin wrapper over /api/v1/recruiter/email-templates. The recruiter
 * editor page (PR-Track-B) uses this; future surfaces — interview
 * invite composer, offer letter generator — can layer on top.
 */
@Injectable({ providedIn: 'root' })
export class EmailTemplatesService {
  private http = inject(HttpClient);
  private base = '/api/v1/recruiter/email-templates';

  catalog(): Observable<EmailTemplateCatalogResponse> {
    return this.http.get<EmailTemplateCatalogResponse>(`${this.base}/catalog`);
  }

  list(): Observable<EmailTemplateListResponse> {
    return this.http.get<EmailTemplateListResponse>(`${this.base}/`);
  }

  get(id: string): Observable<EmailTemplate> {
    return this.http.get<EmailTemplate>(`${this.base}/${id}`);
  }

  create(request: CreateEmailTemplateRequest): Observable<EmailTemplate> {
    return this.http.post<EmailTemplate>(`${this.base}/`, request);
  }

  update(id: string, request: UpdateEmailTemplateRequest): Observable<EmailTemplate> {
    return this.http.patch<EmailTemplate>(`${this.base}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  preview(request: PreviewEmailTemplateRequest): Observable<EmailTemplatePreview> {
    return this.http.post<EmailTemplatePreview>(`${this.base}/preview`, request);
  }
}

export interface EmailTemplate {
  id: string;
  slug: string;
  name: string;
  subject: string;
  bodyMarkdown: string;
  createdByAuthSubject: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface EmailTemplateListResponse {
  items: EmailTemplate[];
}

export interface EmailTemplateCatalogResponse {
  presets: EmailTemplatePreset[];
  supportedMergeFields: string[];
}

export interface EmailTemplatePreset {
  slug: string;
  name: string;
  description: string;
  subject: string;
  bodyMarkdown: string;
  mergeFields: string[];
}

export interface CreateEmailTemplateRequest {
  slug: string;
  name: string;
  subject: string;
  bodyMarkdown: string;
}

export interface UpdateEmailTemplateRequest {
  name?: string;
  subject?: string;
  bodyMarkdown?: string;
}

export interface PreviewEmailTemplateRequest {
  subject: string;
  bodyMarkdown: string;
  mergeFields?: Record<string, string | null>;
}

export interface EmailTemplatePreview {
  subject: string;
  bodyMarkdown: string;
  mergeFields: Record<string, string>;
}
