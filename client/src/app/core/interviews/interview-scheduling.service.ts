import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface InterviewOverviewProvider {
  name: string;
  status: string;
  detail: string;
}

export interface InterviewOverviewEvent {
  id: string;
  submissionId: string;
  candidateName: string;
  candidateEmail: string;
  title: string;
  interviewerName: string;
  provider: string;
  status: string;
  scheduledStartUtc: string;
  scheduledEndUtc: string;
  meetingJoinUrl: string | null;
  externalEventId: string | null;
}

export interface InterviewOverviewResponse {
  providers: InterviewOverviewProvider[];
  events: InterviewOverviewEvent[];
}

@Injectable({ providedIn: 'root' })
export class InterviewSchedulingService {
  private http = inject(HttpClient);

  overview() {
    return this.http.get<InterviewOverviewResponse>(`${environment.apiBase}/api/v1/interviews/overview`);
  }
}
