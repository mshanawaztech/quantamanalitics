import { Injectable, computed, inject } from '@angular/core';

import { MeService, MeResponse } from './me.service';

const PERMISSIONS = {
  recruitingWorkspace: 'recruiting.workspace',
  interviewWorkspace: 'interviews.workspace',
  timeApproval: 'time.approvals',
  payrollBilling: 'payroll.billing',
  clientPortal: 'client.portal',
  candidatePortal: 'candidate.portal',
  platformAdmin: 'platform.admin',
} as const;

type PermissionValue = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];

interface AccessBadge {
  key: PermissionValue;
  label: string;
}

const ACCESS_BADGES: readonly AccessBadge[] = [
  { key: PERMISSIONS.recruitingWorkspace, label: 'Recruiting' },
  { key: PERMISSIONS.interviewWorkspace, label: 'Interviews' },
  { key: PERMISSIONS.timeApproval, label: 'Approvals' },
  { key: PERMISSIONS.payrollBilling, label: 'Payroll' },
  { key: PERMISSIONS.clientPortal, label: 'Client portal' },
  { key: PERMISSIONS.candidatePortal, label: 'Candidate portal' },
  { key: PERMISSIONS.platformAdmin, label: 'Platform admin' },
];

@Injectable({ providedIn: 'root' })
export class AccessService {
  private me = inject(MeService);

  readonly permissions = computed(() => this.resolvePermissions(this.me.data()));
  readonly accessBadges = computed(() =>
    ACCESS_BADGES.filter((badge) => this.permissions().includes(badge.key)),
  );

  readonly canAccessRecruitingWorkspace = computed(() =>
    this.hasPermission(PERMISSIONS.recruitingWorkspace),
  );

  readonly canAccessInterviewWorkspace = computed(() =>
    this.hasPermission(PERMISSIONS.interviewWorkspace),
  );

  readonly canAccessTimeApproval = computed(() =>
    this.hasPermission(PERMISSIONS.timeApproval),
  );

  readonly canAccessPayrollBilling = computed(() =>
    this.hasPermission(PERMISSIONS.payrollBilling),
  );

  readonly canAccessClientPortal = computed(() =>
    this.hasPermission(PERMISSIONS.clientPortal),
  );

  readonly canAccessCandidatePortal = computed(() =>
    this.hasPermission(PERMISSIONS.candidatePortal),
  );

  readonly isPlatformAdmin = computed(() =>
    this.hasPermission(PERMISSIONS.platformAdmin),
  );

  hasPermission(permission: PermissionValue): boolean {
    return this.permissions().includes(permission);
  }

  private resolvePermissions(profile: MeResponse | null): PermissionValue[] {
    if (!profile) {
      return [];
    }

    if (profile.permissions?.length) {
      return profile.permissions as PermissionValue[];
    }

    const permissions = new Set<PermissionValue>();
    const roles = profile.roles ?? [];

    if (roles.includes('PlatformAdmin')) {
      permissions.add(PERMISSIONS.recruitingWorkspace);
      permissions.add(PERMISSIONS.interviewWorkspace);
      permissions.add(PERMISSIONS.timeApproval);
      permissions.add(PERMISSIONS.payrollBilling);
      permissions.add(PERMISSIONS.clientPortal);
      permissions.add(PERMISSIONS.candidatePortal);
      permissions.add(PERMISSIONS.platformAdmin);
    }

    if (roles.includes('Recruiter') || roles.includes('HrAdmin') || roles.includes('Manager')) {
      permissions.add(PERMISSIONS.recruitingWorkspace);
    }

    if (roles.includes('Recruiter') || roles.includes('HrAdmin') || roles.includes('Interviewer') || roles.includes('Manager')) {
      permissions.add(PERMISSIONS.interviewWorkspace);
    }

    if (roles.includes('Client') || roles.includes('PayrollAdmin') || roles.includes('Manager')) {
      permissions.add(PERMISSIONS.timeApproval);
    }

    if (roles.includes('PayrollAdmin') || roles.includes('Manager')) {
      permissions.add(PERMISSIONS.payrollBilling);
    }

    if (roles.includes('Client')) {
      permissions.add(PERMISSIONS.clientPortal);
    }

    if (roles.includes('Candidate')) {
      permissions.add(PERMISSIONS.candidatePortal);
    }

    return [...permissions];
  }
}
