import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, of } from 'rxjs';

import { environment } from '../../../environments/environment';

export interface PublicJobListItem {
  id: string;
  title: string;
  slug: string;
  location: string;
  summary: string;
  postedOnUtc: string;
}

export interface PublicJobDetail extends PublicJobListItem {
  description: string;
}

export interface PublicJobApplicationResponse {
  jobId: string;
  jobSlug: string;
  email: string;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class PublicJobsService {
  private http = inject(HttpClient);

  list() {
    return this.http
      .get<PublicJobListItem[]>(`${environment.apiBase}/api/v1/jobs`)
      .pipe(catchError(() => of([])));
  }

  detail(slug: string) {
    return this.http
      .get<PublicJobDetail>(`${environment.apiBase}/api/v1/jobs/${slug}`)
      .pipe(catchError(() => of(null)));
  }

  apply(slug: string, request: { fullName: string; email: string; note: string }) {
    return this.http.post<PublicJobApplicationResponse>(
      `${environment.apiBase}/api/v1/jobs/${slug}/apply`,
      request,
    );
  }
}
