import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import { HealthService } from './core/health/health.service';

@Component({
  selector: 'app-status-panel',
  templateUrl: './status-panel.component.html',
  styleUrl: './status-panel.component.scss',
})
export class StatusPanelComponent {
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
