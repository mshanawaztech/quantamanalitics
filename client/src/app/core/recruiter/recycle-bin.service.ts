import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * Client wrapper for the recruiter recycle-bin shipped in Story 61.
 * Today the only supported resource is deleted Jobs; as more entities
 * adopt ISoftDeletable the service expands resource-by-resource — never
 * with a single "list everything deleted" call, because the UI needs to
 * group restoration affordances by aggregate type.
 */
@Injectable({ providedIn: 'root' })
export class RecycleBinService {
  private http = inject(HttpClient);
  private base = '/api/v1/recruiter/recycle-bin';

  listJobs(): Observable<RecycleBinJobsResponse> {
    return this.http.get<RecycleBinJobsResponse>(`${this.base}/jobs`);
  }

  restoreJob(id: string): Observable<RecycleBinJobItem> {
    return this.http.post<RecycleBinJobItem>(`${this.base}/jobs/${id}/restore`, null);
  }

  softDeleteJob(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/jobs/${id}`);
  }
}

export interface RecycleBinJobsResponse {
  items: RecycleBinJobItem[];
  count: number;
}

export interface RecycleBinJobItem {
  id: string;
  title: string;
  slug: string;
  deletedAtUtc: string;
  deletedByAuthSubject: string | null;
}
