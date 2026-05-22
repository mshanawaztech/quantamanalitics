import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Management view of onboarding progress across the tenant. Backed by
 * GET /api/v1/onboarding/summary, which aggregates each consultant's
 * checklist completion (recruiting access required).
 */
@Injectable({ providedIn: 'root' })
export class OnboardingDashboardService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/api/v1/onboarding`;

  summary(): Observable<OnboardingSummaryResponse> {
    return this.http.get<OnboardingSummaryResponse>(`${this.base}/summary`);
  }
}

export interface OnboardingSummaryResponse {
  consultants: OnboardingSummaryRow[];
  totals: OnboardingSummaryTotals;
}

export interface OnboardingSummaryRow {
  candidateProfileId: string;
  name: string;
  email: string;
  total: number;
  completed: number;
  inReview: number;
  pending: number;
  completionPercent: number;
  status: string;
  lastActivityUtc: string;
}

export interface OnboardingSummaryTotals {
  consultants: number;
  complete: number;
  inProgress: number;
  overallCompletionPercent: number;
}
