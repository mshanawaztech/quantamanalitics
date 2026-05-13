import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

import { environment } from '../../../environments/environment';
import { ParsedResumeResult } from '../resume/resume-parse.models';

export interface CandidateProfile {
  id: string;
  tenantId: string;
  authSubject: string;
  email: string;
  fullName: string | null;
  phoneNumber: string | null;
  headline: string | null;
  summary: string | null;
  resumeFileName: string | null;
  resumeUploadedAtUtc: string | null;
}

export interface CandidateApplication {
  id: string;
  jobId: string;
  jobTitle: string;
  jobSlug: string;
  location: string;
  status: string;
  note: string | null;
  appliedAtUtc: string;
  updatedAtUtc: string;
}

export interface CandidateTimelineItem {
  id: string;
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

export interface UpdateCandidateProfileRequest {
  email: string;
  fullName: string | null;
  phoneNumber: string | null;
  headline: string | null;
  summary: string | null;
}

@Injectable({ providedIn: 'root' })
export class CandidateProfileService {
  private http = inject(HttpClient);

  load() {
    return this.http.get<CandidateProfile>(
      `${environment.apiBase}/api/v1/candidate/profile`,
    );
  }

  save(request: UpdateCandidateProfileRequest) {
    return this.http.put<CandidateProfile>(
      `${environment.apiBase}/api/v1/candidate/profile`,
      request,
    );
  }

  uploadResume(file: File) {
    const body = new FormData();
    body.append('file', file);

    return this.http.post<CandidateProfile>(
      `${environment.apiBase}/api/v1/candidate/profile/resume`,
      body,
    );
  }

  parseResume(file: File) {
    const body = new FormData();
    body.append('file', file);

    return this.http.post<ParsedResumeResult>(
      `${environment.apiBase}/api/v1/candidate/profile/resume/parse`,
      body,
    );
  }

  applications() {
    return this.http.get<{ items: CandidateApplication[] }>(
      `${environment.apiBase}/api/v1/candidate/profile/applications`,
    );
  }

  timeline() {
    return this.http.get<{ items: CandidateTimelineItem[] }>(
      `${environment.apiBase}/api/v1/candidate/profile/timeline`,
    );
  }
}
