import { Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { AvatarModule } from 'primeng/avatar';
import { MenuModule } from 'primeng/menu';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { API_STATUS_DISPLAY, ApiStatusService } from '../../core/api-status.service';
import { APP_INFO } from '../../core/app-info';
import { SyntheticDataSetting } from '../../core/session/synthetic-data';
import { TechnicianSession } from '../../core/session/technician-session';
import { SHELL_MENU } from './menu-items';

/**
 * The frame every screen lives in: header bar, side menu and the routed page (hi-fi mockup).
 */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    FormsModule,
    AvatarModule,
    MenuModule,
    TagModule,
    ToggleSwitchModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly apiStatus = inject(ApiStatusService);
  private readonly session = inject(TechnicianSession);
  private readonly router = inject(Router);

  protected readonly menu = SHELL_MENU;
  protected readonly appInfo = APP_INFO;

  protected readonly apiDisplay = computed(() => API_STATUS_DISPLAY[this.apiStatus.status()]);

  /** Shared with Home and Search, which hide synthetic batches when it's off. */
  protected readonly showSynthetic = inject(SyntheticDataSetting).show;

  protected readonly technicianName = computed(() => this.session.name() ?? '');
  protected readonly technicianInitials = computed(() =>
    this.technicianName()
      .split(/[\s.]+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0].toUpperCase())
      .join(''),
  );

  /** The technician chip's menu (M1-42). */
  protected readonly technicianMenu: MenuItem[] = [
    {
      label: 'Switch technician',
      icon: 'pi pi-user-edit',
      command: () =>
        this.router.navigate(['/start'], { queryParams: { returnUrl: this.router.url } }),
    },
  ];
}
