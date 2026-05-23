import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Recruiter reporting. Backed by /api/v1/recruiter/reports.
 * Burn-rate shows payable hours logged per week (submitted + approved
 * timesheets) plus a per-consultant breakdown and an estimated cost.
 */
@Injectable({ providedIn: 'root' })
export class ReportingService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/api/v1/recruiter/reports`;

  burnRate(weeks = 8): Observable<BurnRateResponse> {
    const params = new HttpParams().set('weeks', weeks);
    return this.http.get<BurnRateResponse>(`${this.base}/burn-rate`, { params });
  }
}

export interface BurnRateWeek {
  weekStartUtc: string;
  payableHours: number;
  consultants: number;
}

export interface BurnRateConsultant {
  consultant: string;
  payableHours: number;
}

export interface BurnRateResponse {
  weeks: BurnRateWeek[];
  consultants: BurnRateConsultant[];
  totalPayableHours: number;
  estimatedCost: number;
  hourlyRateUsed: number;
  currency: string;
}
