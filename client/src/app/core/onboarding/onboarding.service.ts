import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Consultant self-service onboarding. Backed by /api/v1/onboarding/me:
 *   GET  /items            the consultant's phased checklist
 *   POST /apply-template   seed the default template for a worker type
 *   POST /items/{id}/submit  mark a step submitted for review
 *
 * ConsultantType is sent as a number (Contractor=0, FullTime=1, Vendor=2)
 * because the API binds enums numerically.
 */
@Injectable({ providedIn: 'root' })
export class OnboardingService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/api/v1/onboarding/me`;

  mine(): Observable<OnboardingChecklistResponse> {
    return this.http.get<OnboardingChecklistResponse>(`${this.base}/items`);
  }

  applyTemplate(consultantType: ConsultantType): Observable<OnboardingChecklistResponse> {
    return this.http.post<OnboardingChecklistResponse>(`${this.base}/apply-template`, { consultantType });
  }

  submit(itemId: string, note: string | null): Observable<OnboardingItem> {
    return this.http.post<OnboardingItem>(`${this.base}/items/${itemId}/submit`, { note });
  }
}

export enum ConsultantType {
  Contractor = 0,
  FullTime = 1,
  Vendor = 2,
}

export type OnboardingPhase =
  | 'IdentityEligibility'
  | 'ProfessionalBackground'
  | 'Agreements'
  | 'Screening'
  | 'PayrollLogistics'
  | 'General';

export type OnboardingItemStatus = 'Pending' | 'Submitted' | 'Approved';

export interface OnboardingItem {
  id: string;
  candidateProfileId: string;
  itemType: string;
  title: string;
  instructions: string | null;
  status: OnboardingItemStatus;
  candidateNote: string | null;
  reviewerNote: string | null;
  assignedAtUtc: string;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  updatedAtUtc: string;
  phase: OnboardingPhase;
  isRequired: boolean;
  sortOrder: number;
}

export interface OnboardingChecklistResponse {
  items: OnboardingItem[];
}
