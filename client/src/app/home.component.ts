import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { StatusPanelComponent } from './status-panel.component';
import { QaLogoComponent } from './core/ui';

@Component({
  selector: 'app-home',
  imports: [QaLogoComponent, RouterLink, StatusPanelComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class HomeComponent {}
