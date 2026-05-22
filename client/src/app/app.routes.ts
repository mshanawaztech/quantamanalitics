import { Routes } from '@angular/router';
import { AboutComponent } from './about.component';
import { AdminAuditConsoleComponent } from './admin-audit-console.component';
import { ContactComponent } from './contact.component';
import { CandidateDashboardComponent } from './candidate-dashboard.component';
import { CandidatePortalComponent } from './candidate-portal.component';
import { ClientDashboardComponent } from './client-dashboard.component';
import { JobDetailComponent } from './job-detail.component';
import { JobsListComponent } from './jobs-list.component';
import { MarketingShellComponent } from './marketing-shell.component';
import { NotFoundComponent } from './not-found.component';
import { OnboardingDashboardComponent } from './onboarding-dashboard.component';
import { RecruiterDashboardComponent } from './recruiter-dashboard.component';
import { RecruiterEmailTemplatesComponent } from './recruiter-email-templates.component';
import { RecruiterPipelineComponent } from './recruiter-pipeline.component';
import { RecruiterRecycleBinComponent } from './recruiter-recycle-bin.component';
import { ContractorDashboardComponent } from './contractor-dashboard.component';
import { ContractorInvoicesComponent } from './contractor-invoices.component';
import { HomeComponent } from './home.component';
import { InterviewSchedulingComponent } from './interview-scheduling.component';
import { ServicesComponent } from './services.component';
import { SettingsBrandingComponent } from './settings-branding.component';
import { StyleGuideComponent } from './style-guide.component';
import {
  DpaComponent,
  PrivacyComponent,
  SecurityComponent,
  TermsComponent,
  TrustCenterComponent,
} from './trust-center.component';
import { LoginComponent } from './core/auth/login.component';

export const routes: Routes = [
  {
    path: '',
    component: MarketingShellComponent,
    children: [
      {
        path: '',
        component: HomeComponent,
      },
      {
        path: 'about',
        component: AboutComponent,
      },
      {
        path: 'services',
        component: ServicesComponent,
      },
      {
        path: 'contact',
        component: ContactComponent,
      },
      {
        path: 'jobs',
        component: JobsListComponent,
      },
      {
        path: 'jobs/:slug',
        component: JobDetailComponent,
      },
      {
        path: 'client',
        component: ClientDashboardComponent,
      },
      {
        path: 'candidate',
        component: CandidatePortalComponent,
      },
      {
        path: 'candidate/portal',
        component: CandidatePortalComponent,
      },
      {
        path: 'candidate/dashboard',
        component: CandidateDashboardComponent,
      },
      {
        path: 'admin/audit',
        component: AdminAuditConsoleComponent,
      },
      {
        path: 'contractor',
        component: ContractorDashboardComponent,
      },
      {
        path: 'contractor/invoices',
        component: ContractorInvoicesComponent,
      },
      {
        path: 'recruiter',
        component: RecruiterDashboardComponent,
      },
      {
        path: 'onboarding',
        component: OnboardingDashboardComponent,
      },
      {
        path: 'recruiter/pipeline',
        component: RecruiterPipelineComponent,
      },
      {
        path: 'recruiter/email-templates',
        component: RecruiterEmailTemplatesComponent,
      },
      {
        path: 'recruiter/recycle-bin',
        component: RecruiterRecycleBinComponent,
      },
      {
        path: 'interviews',
        component: InterviewSchedulingComponent,
      },
      {
        path: 'trust',
        component: TrustCenterComponent,
      },
      {
        path: 'privacy',
        component: PrivacyComponent,
      },
      {
        path: 'terms',
        component: TermsComponent,
      },
      {
        path: 'security',
        component: SecurityComponent,
      },
      {
        path: 'dpa',
        component: DpaComponent,
      },
      {
        path: 'settings/branding',
        component: SettingsBrandingComponent,
      },
      {
        path: 'style-guide',
        component: StyleGuideComponent,
      },
      {
        path: '**',
        component: NotFoundComponent,
      },
    ],
  },
  {
    path: 'login',
    component: LoginComponent,
  },
];
