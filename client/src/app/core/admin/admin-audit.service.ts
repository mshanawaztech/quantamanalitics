import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface AuditLogEntry {
  id: string;
  authSubject: string | null;
  action: string;
  entityType: string;
  entityId: string;
  metadataJson: string | null;
  recordedAtUtc: string;
}

export interface AuditLogQueryResponse {
  items: AuditLogEntry[];
  pageSize: number;
}

export interface AuditLogQuery {
  authSubject?: string | null;
  action?: string | null;
  entityType?: string | null;
  entityId?: string | null;
  from?: string | null;
  to?: string | null;
  pageSize?: number | null;
}

@Injectable({ providedIn: 'root' })
export class AdminAuditService {
  private readonly http = inject(HttpClient);

  query(request?: AuditLogQuery) {
    let params = new HttpParams();

    if (request?.authSubject?.trim()) {
      params = params.set('authSubject', request.authSubject.trim());
    }

    if (request?.action?.trim()) {
      params = params.set('action', request.action.trim());
    }

    if (request?.entityType?.trim()) {
      params = params.set('entityType', request.entityType.trim());
    }

    if (request?.entityId?.trim()) {
      params = params.set('entityId', request.entityId.trim());
    }

    if (request?.from?.trim()) {
      params = params.set('from', request.from.trim());
    }

    if (request?.to?.trim()) {
      params = params.set('to', request.to.trim());
    }

    if (request?.pageSize) {
      params = params.set('pageSize', String(request.pageSize));
    }

    return this.http.get<AuditLogQueryResponse>(
      `${environment.apiBase}/api/v1/admin/audit-log`,
      { params },
    );
  }
}
