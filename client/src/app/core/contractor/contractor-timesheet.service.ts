import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface ContractorTimesheetEntry {
  workDate: string;
  hours: number;
  entryType: 'Work' | 'PaidTimeOff';
  notes: string | null;
}

export interface ContractorTimesheet {
  id: string | null;
  tenantId: string;
  contractorAuthSubject: string;
  contractorEmail: string;
  weekStartUtc: string;
  status: 'Draft' | 'Submitted' | 'Approved' | 'Rejected';
  reviewedByAuthSubject: string | null;
  reviewNote: string | null;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  totalHours: number;
  totals: TimesheetTotals;
  entries: ContractorTimesheetEntry[];
}

export interface TimesheetTotals {
  workHours: number;
  paidTimeOffHours: number;
  regularHours: number;
  overtimeHours: number;
  payableHours: number;
}

export interface UpsertContractorTimesheetRequest {
  weekStartUtc: string;
  entries: ContractorTimesheetEntry[];
}

export interface ContractorTimesheetSummary {
  id: string;
  weekStartUtc: string;
  status: 'Draft' | 'Submitted' | 'Approved' | 'Rejected';
  totalHours: number;
  payableHours: number;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
}

export interface ContractorTimesheetList {
  items: ContractorTimesheetSummary[];
}

@Injectable({ providedIn: 'root' })
export class ContractorTimesheetService {
  private http = inject(HttpClient);

  /** All of the current contractor's timesheets, newest week first. */
  listMine() {
    return this.http.get<ContractorTimesheetList>(
      `${environment.apiBase}/api/v1/contractor/timesheets`,
    );
  }

  current(weekStartUtc: string) {
    const params = new HttpParams().set('weekStart', weekStartUtc);
    return this.http.get<ContractorTimesheet>(
      `${environment.apiBase}/api/v1/contractor/timesheets/current`,
      { params },
    );
  }

  save(request: UpsertContractorTimesheetRequest) {
    return this.http.put<ContractorTimesheet>(
      `${environment.apiBase}/api/v1/contractor/timesheets/current`,
      request,
    );
  }

  submit(request: UpsertContractorTimesheetRequest) {
    return this.http.post<ContractorTimesheet>(
      `${environment.apiBase}/api/v1/contractor/timesheets/current/submit`,
      request,
    );
  }
}
