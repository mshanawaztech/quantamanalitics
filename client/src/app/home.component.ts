import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { HealthService } from './core/health/health.service';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';

@Component({
  selector: 'app-home',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class HomeComponent {
  protected health = inject(HealthService);
  protected auth = inject(AuthService);
  protected me = inject(MeService);
  private router = inject(Router);

  login(): void {
    void this.router.navigateByUrl('/login');
  }

  logout(): void {
    this.auth.logout();
  }
}
