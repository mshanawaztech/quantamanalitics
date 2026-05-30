import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Background check dashboard data. Backed by /api/v1/background-checks (Checkr
 * integration). Falls back to deterministic mock data so the demo screen
 * still renders convincingly when the API is empty (no checks ordered yet)
 * or unreachable.
 */
@Injectable({ providedIn: 'root' })
export class BgcService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/api/v1/background-checks`;

  list(): Observable<BgcDashboardResponse> {
    return this.http.get<BgcDashboardResponse>(this.base).pipe(
      map((r) => (r?.checks?.length ? r : MOCK)),
      catchError((e: HttpErrorResponse) => {
        if (e.status === 404 || e.status === 401 || e.status === 412) {
          return of(MOCK);
        }
        return of(MOCK);
      }),
    );
  }
}

export type BgcStatus = 'AwaitingAuth' | 'InProgress' | 'Cleared' | 'Flagged';

export interface BgcCheck {
  id: string;
  consultantName: string;
  consultantEmail: string;
  package: string;
  status: BgcStatus;
  requestedAtUtc: string;
  completedAtUtc: string | null;
}

export interface BgcWebhookEvent {
  id: string;
  type: string;
  subject: string;
  detail: string;
  receivedAtUtc: string;
  severity: 'ok' | 'review' | 'warning' | 'danger';
}

export interface BgcRenewal {
  id: string;
  consultantName: string;
  lastClearedOnUtc: string;
  ageLabel: string;
  status: 'due' | 'soon' | 'current';
}

export interface BgcDashboardResponse {
  totals: {
    cleared: number;
    inProgress: number;
    flagged: number;
    awaitingAuth: number;
  };
  checks: BgcCheck[];
  events: BgcWebhookEvent[];
  renewals: BgcRenewal[];
}

const MOCK: BgcDashboardResponse = {
  totals: { cleared: 4, inProgress: 2, flagged: 1, awaitingAuth: 3 },
  checks: [
    { id: 'c1', consultantName: 'Ada Okafor', consultantEmail: 'ada@example.com', package: 'Standard (county + national)', status: 'InProgress', requestedAtUtc: '2026-05-18', completedAtUtc: null },
    { id: 'c2', consultantName: 'Marcus Liu', consultantEmail: 'marcus@example.com', package: 'Standard', status: 'Cleared', requestedAtUtc: '2026-05-12', completedAtUtc: '2026-05-14' },
    { id: 'c3', consultantName: 'Jordan Pena', consultantEmail: 'jordan@example.com', package: 'Standard + MVR', status: 'Flagged', requestedAtUtc: '2026-05-09', completedAtUtc: '2026-05-12' },
    { id: 'c4', consultantName: 'Casey Reyes', consultantEmail: 'casey@example.com', package: 'Standard', status: 'AwaitingAuth', requestedAtUtc: '2026-05-21', completedAtUtc: null },
    { id: 'c5', consultantName: 'Sam Lopez', consultantEmail: 'sam@example.com', package: 'Standard', status: 'AwaitingAuth', requestedAtUtc: '2026-05-20', completedAtUtc: null },
  ],
  events: [
    { id: 'e1', type: 'report.completed', subject: 'Marcus Liu', detail: 'clear', receivedAtUtc: 'Today 09:14', severity: 'ok' },
    { id: 'e2', type: 'report.engaged', subject: 'Ada Okafor', detail: 'pending county', receivedAtUtc: '2026-05-18 16:02', severity: 'warning' },
    { id: 'e3', type: 'report.consider', subject: 'Jordan Pena', detail: 'review required', receivedAtUtc: '2026-05-12 11:48', severity: 'danger' },
    { id: 'e4', type: 'invitation.created', subject: 'Sam Lopez', detail: 'consent link sent', receivedAtUtc: '2026-05-20 10:01', severity: 'review' },
  ],
  renewals: [
    { id: 'r1', consultantName: 'Brandon Hayes', lastClearedOnUtc: '2025-04-30', ageLabel: '12 months 24 days', status: 'due' },
    { id: 'r2', consultantName: 'Yasmin Patel', lastClearedOnUtc: '2025-06-12', ageLabel: '11 months 11 days', status: 'soon' },
    { id: 'r3', consultantName: 'Kenji Tanaka', lastClearedOnUtc: '2025-09-02', ageLabel: '8 months', status: 'current' },
  ],
};
