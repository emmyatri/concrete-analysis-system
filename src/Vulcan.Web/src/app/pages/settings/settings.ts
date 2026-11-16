import { Component, DestroyRef, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { Observable, finalize } from 'rxjs';

import { describeApiError } from '../../core/api/api-errors';
import { CloudFolderSetting } from '../../core/api/models';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { desktopWebView } from '../../core/desktop-host';

/**
 * Settings for this computer (SCRUM-214). The cloud folder is where "Save to cloud" on a batch report
 * puts the PDF and data export. The desktop app shows the folder picker; the choice is saved through the API.
 */
@Component({
  selector: 'app-settings',
  imports: [ButtonModule],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class SettingsPage {
  private readonly api = inject(VulcanApiService);
  private readonly webview = desktopWebView();

  protected readonly isDesktopHost = !!this.webview;
  protected readonly status = signal<'loading' | 'loaded' | 'error'>('loading');
  protected readonly cloudFolder = signal<CloudFolderSetting>({ path: null, exists: false });
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  constructor() {
    this.load();

    const webview = this.webview;
    if (webview) {
      const listener = (event: MessageEvent) => this.onDesktopMessage(event.data);
      webview.addEventListener('message', listener);
      inject(DestroyRef).onDestroy(() => webview.removeEventListener('message', listener));
    }
  }

  /** Opens the folder picker in the desktop app, which answers with "cloud-folder-chosen". */
  protected chooseFolder(): void {
    this.webview?.postMessage({ type: 'choose-cloud-folder' });
  }

  protected clearFolder(): void {
    this.save(this.api.clearCloudFolder());
  }

  private onDesktopMessage(data: unknown): void {
    const message = data as { type?: unknown; path?: unknown } | null;
    if (message?.type === 'cloud-folder-chosen' && typeof message.path === 'string') {
      this.save(this.api.setCloudFolder(message.path));
    }
  }

  private load(): void {
    this.api.getCloudFolder().subscribe({
      next: (setting) => {
        this.cloudFolder.set(setting);
        this.status.set('loaded');
      },
      error: () => this.status.set('error'),
    });
  }

  private save(request: Observable<void>): void {
    this.saving.set(true);
    this.saveError.set(null);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => this.load(),
      error: (error: unknown) => {
        const problem = describeApiError(error);
        this.saveError.set(problem.fieldErrors[0]?.message ?? problem.message);
      },
    });
  }
}
