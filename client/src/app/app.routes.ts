import { Routes } from '@angular/router';
import { AboutComponent } from './about.component';
import { ContactComponent } from './contact.component';
import { CandidateDashboardComponent } from './candidate-dashboard.component';
import { ClientDashboardComponent } from './client-dashboard.component';
import { JobDetailComponent } from './job-detail.component';
import { JobsListComponent } from './jobs-list.component';
import { MarketingShellComponent } from './marketing-shell.component';
import { NotFoundComponent } from './not-found.component';
import { RecruiterDashboardComponent } from './recruiter-dashboard.component';
import { ContractorDashboardComponent } from './contractor-dashboard.component';
import { HomeComponent } from './home.component';
import { InterviewSchedulingComponent } from './interview-scheduling.component';
import { ServicesComponent } from './services.component';
import { AccessibilityComponent } from './accessibility.component';
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
        component: CandidateDashboardComponent,
      },
      {
        path: 'contractor',
        component: ContractorDashboardComponent,
      },
      {
        path: 'recruiter',
        component: RecruiterDashboardComponent,
      },
      {
        path: 'interviews',
        component: InterviewSchedulingComponent,
      },
      {
        path: 'accessibility',
        component: AccessibilityComponent,
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
