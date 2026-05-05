import { Routes } from '@angular/router';
import { AboutComponent } from './about.component';
import { ContactComponent } from './contact.component';
import { MarketingShellComponent } from './marketing-shell.component';
import { HomeComponent } from './home.component';
import { ServicesComponent } from './services.component';
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
    ],
  },
  {
    path: 'login',
    component: LoginComponent,
  },
];
