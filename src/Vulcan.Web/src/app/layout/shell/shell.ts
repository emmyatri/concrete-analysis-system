import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AvatarModule } from 'primeng/avatar';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { APP_INFO } from '../../core/app-info';
import { ApiStatusService } from '../../core/api-status.service';
import { SHELL_MENU } from './menu-items';

/** Placeholder until the Start session screen (M1-42) sets the real technician. */
const PLACEHOLDER_TECHNICIAN = 'Technician';

const API_STATUS_DISPLAY = {
  starting: { label: 'Starting local service…', severity: 'warn' },
  ready: { label: 'Local API ready', severity: 'success' },
  failed: { label: 'Local API unavailable', severity: 'danger' },
} as const;

/**
 * The frame every screen lives in: header bar, side menu and the routed page (hi-fi mockup).
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, AvatarModule, TagModule, ToggleSwitchModule],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly apiStatus = inject(ApiStatusService);

  protected readonly menu = SHELL_MENU;
  protected readonly appInfo = APP_INFO;

  protected readonly apiDisplay = computed(() => API_STATUS_DISPLAY[this.apiStatus.status()]);

  /** Not wired to data yet: a later ticket filters screens by this. */
  protected readonly showSynthetic = signal(true);

  protected readonly technicianName = signal(PLACEHOLDER_TECHNICIAN);
  protected readonly technicianInitials = computed(() =>
    this.technicianName()
      .split(/[\s.]+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0].toUpperCase())
      .join(''),
  );
}
