import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HealthService } from './core/health/health.service';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected health = inject(HealthService);
  protected auth = inject(AuthService);
  protected me = inject(MeService);

  login(): void {
    this.auth.loginWithRedirect();
  }

  logout(): void {
    this.auth.logout();
  }
}
