import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { retry, timer } from 'rxjs';

export type ApiStatus = 'starting' | 'ready' | 'failed';

/** How each status shows in the green / amber / red chip (header and Start session). */
export const API_STATUS_DISPLAY = {
  starting: { label: 'Starting local service…', severity: 'warn' },
  ready: { label: 'Local API ready', severity: 'success' },
  failed: { label: 'Local API unavailable', severity: 'danger' },
} as const;

/**
 * Tracks whether the local API is answering (status chip).
 * Checks /api/health once on start, retrying for a few seconds before reporting failure.
 */
@Injectable({ providedIn: 'root' })
export class ApiStatusService {
  private readonly http = inject(HttpClient);

  private readonly _status = signal<ApiStatus>('starting');
  readonly status = this._status.asReadonly();

  constructor() {
    this.check();
  }

  /** Checks again, e.g. from the Retry button on Start session. */
  retry(): void {
    this._status.set('starting');
    this.check();
  }

  private check(): void {
    this.http
      .get('/api/health')
      .pipe(retry({ count: 5, delay: () => timer(1000) }))
      .subscribe({
        next: () => this._status.set('ready'),
        error: () => this._status.set('failed'),
      });
  }
}
