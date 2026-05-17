import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Wrapper for the recruiter resume-parse endpoint (PR-82 backend).
 * The endpoint is stateless — nothing is persisted; we send the file,
 * get back the parser's best guess, and the recruiter decides what to
 * do with the extracted fields next.
 */
@Injectable({ providedIn: 'root' })
export class ResumeParseService {
  private http = inject(HttpClient);

  parse(file: File): Observable<ParsedResume> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ParsedResume>(
      `${environment.apiBase}/api/v1/recruiter/resume/parse`,
      form,
    );
  }
}

export interface ParsedResume {
  fullName: string | null;
  email: string | null;
  phoneNumber: string | null;
  headline: string | null;
  skills: string[];
  workHistory: ParsedResumeWorkItem[];
}

export interface ParsedResumeWorkItem {
  employer: string;
  title: string;
  startDate: string | null;
  endDate: string | null;
}
