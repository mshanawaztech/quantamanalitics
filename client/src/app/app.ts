import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HealthService } from './core/health/health.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected health = inject(HealthService);
}
