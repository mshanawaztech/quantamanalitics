import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { StatusPanelComponent } from './status-panel.component';

@Component({
  selector: 'app-home',
  imports: [RouterLink, StatusPanelComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class HomeComponent {}
