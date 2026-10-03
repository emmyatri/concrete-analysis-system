import { Component, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';

import { API_STATUS_DISPLAY, ApiStatusService } from '../../core/api-status.service';
import { technicianNameField } from '../../core/session/technician-name';
import { ALLOW_START_WITHOUT_API, TechnicianSession } from '../../core/session/technician-session';

/**
 * Start session (M1-42, mockup "01 · Start session"): asks who is using the app before any other screen.
 * Also opened from the top bar's "Switch technician".
 */
@Component({
  selector: 'app-start-session',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, TagModule],
  templateUrl: './start-session.html',
  styleUrl: './start-session.scss',
})
export class StartSession {
  private readonly session = inject(TechnicianSession);
  private readonly apiStatus = inject(ApiStatusService);
  private readonly router = inject(Router);
  private readonly allowWithoutApi = inject(ALLOW_START_WITHOUT_API);

  /** Where to go after starting (query param, set by the guard or Switch technician). */
  readonly returnUrl = input<string>();

  protected readonly name = new FormControl(this.session.name() ?? '', {
    nonNullable: true,
    validators: [technicianNameField],
  });
  protected readonly form = new FormGroup({ name: this.name });

  protected readonly saving = signal(false);
  protected readonly saveFailed = signal(false);

  protected readonly status = this.apiStatus.status;
  protected readonly statusDisplay = computed(() => API_STATUS_DISPLAY[this.status()]);

  /** Re-runs the computed values below whenever the field changes. */
  private readonly nameChange = toSignal(this.name.events, { initialValue: null });

  /** The red message, shown once the operator has typed or left the field. */
  protected readonly error = computed(() => {
    this.nameChange();
    const shown = this.name.dirty || this.name.touched;
    return shown ? ((this.name.errors?.['technicianName']?.message as string | undefined) ?? null) : null;
  });

  protected readonly canStart = computed(() => {
    this.nameChange();
    const apiOk = this.status() === 'ready' || (this.status() === 'failed' && this.allowWithoutApi);
    return this.name.valid && apiOk && !this.saving();
  });

  constructor() {
    // Fill in the last name used on this computer, unless the operator already started typing.
    if (!this.session.isStarted()) {
      this.session.lastUsed().subscribe((last) => {
        if (last && this.name.pristine) this.name.setValue(last);
      });
    }
  }

  protected retry(): void {
    this.apiStatus.retry();
  }

  protected start(): void {
    this.name.markAsTouched();
    if (!this.canStart()) return;

    this.saving.set(true);
    this.saveFailed.set(false);

    this.session.start(this.name.value).subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigateByUrl(this.safeReturnUrl());
      },
      error: () => {
        this.saving.set(false);
        this.saveFailed.set(true);
      },
    });
  }

  /** Only paths inside the app, never another site. */
  private safeReturnUrl(): string {
    const url = this.returnUrl();
    return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/start') ? url : '/';
  }
}
