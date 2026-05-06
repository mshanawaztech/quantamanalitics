import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface ClientApprovalTimeEntry {
  workDate: string;
  hours: number;
  entryType: string;
  notes: string | null;
}

export interface ClientApprovalTimesheet {
  id: string;
  contractorAuthSubject: string;
  contractorEmail: string;
  weekStartUtc: string;
  status: 'Submitted' | 'Approved' | 'Rejected';
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  reviewNote: string | null;
  totalHours: number;
  totals: TimesheetTotals;
  entries: ClientApprovalTimeEntry[];
}

export interface TimesheetTotals {
  workHours: number;
  paidTimeOffHours: number;
  regularHours: number;
  overtimeHours: number;
  payableHours: number;
}

export interface ClientApprovalTimesheetsResponse {
  items: ClientApprovalTimesheet[];
}

@Injectable({ providedIn: 'root' })
export class ClientApprovalService {
  private http = inject(HttpClient);

  timesheets() {
    return this.http.get<ClientApprovalTimesheetsResponse>(
      `${environment.apiBase}/api/v1/client/approvals/timesheets`,
    );
  }

  approve(timesheetId: string, reviewNote: string | null) {
    return this.http.post<ClientApprovalTimesheet>(
      `${environment.apiBase}/api/v1/client/approvals/timesheets/${timesheetId}/approve`,
      { reviewNote },
    );
  }

  reject(timesheetId: string, reviewNote: string) {
    return this.http.post<ClientApprovalTimesheet>(
      `${environment.apiBase}/api/v1/client/approvals/timesheets/${timesheetId}/reject`,
      { reviewNote },
    );
  }
}
