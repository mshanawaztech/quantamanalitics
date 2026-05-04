import { Routes } from '@angular/router';
import { HomeComponent } from './home.component';
import { LoginComponent } from './core/auth/login.component';

export const routes: Routes = [
  {
    path: '',
    component: HomeComponent,
  },
  {
    path: 'login',
    component: LoginComponent,
  },
];
