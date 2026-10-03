import { Injectable, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';

import { Material, SpecificGravity, ValidationLimit } from './models';
import { VulcanApiService } from './vulcan-api.service';

export type ReferenceDataStatus = 'idle' | 'loading' | 'ready' | 'failed';

/** The Reject and Warn rows for one key. Either can be missing. */
export interface LimitPair {
  reject?: ValidationLimit;
  warn?: ValidationLimit;
}

/**
 * The validation rules and specific gravities, loaded once at startup and shared with every form (M1-60).
 * Forms read ranges and messages from here, so no numbers are hard-coded in the UI (M1-45).
 */
@Injectable({ providedIn: 'root' })
export class ReferenceDataService {
  private readonly api = inject(VulcanApiService);

  private readonly _status = signal<ReferenceDataStatus>('idle');
  readonly status = this._status.asReadonly();

  private readonly _limits = signal<ValidationLimit[]>([]);
  readonly limits = this._limits.asReadonly();

  private readonly _gravities = signal<SpecificGravity[]>([]);

  /** Material → specific gravity, for the water/cementitious ratio. */
  readonly specificGravities = computed(
    () => new Map<Material, number>(this._gravities().map((row) => [row.material, row.value])),
  );

  private readonly limitsByKey = computed(() => {
    const byKey = new Map<string, LimitPair>();
    for (const limit of this._limits()) {
      const pair = byKey.get(limit.key) ?? {};
      if (limit.severity === 'Reject') pair.reject = limit;
      else pair.warn = limit;
      byKey.set(limit.key, pair);
    }
    return byKey;
  });

  /** The Reject and Warn rows for a key such as "Composition.CementPct", or undefined if none. */
  limitFor(key: string): LimitPair | undefined {
    return this.limitsByKey().get(key);
  }

  /** Loads both lists. Does nothing if they're loaded or loading, so it's safe to call from anywhere. */
  load(): void {
    if (this._status() === 'loading' || this._status() === 'ready') return;
    this.fetch();
  }

  /** Loads again, e.g. after the local service failed to start. */
  reload(): void {
    if (this._status() === 'loading') return;
    this.fetch();
  }

  private fetch(): void {
    this._status.set('loading');
    forkJoin({ limits: this.api.getValidationLimits(), gravities: this.api.getSpecificGravities() }).subscribe({
      next: ({ limits, gravities }) => {
        this._limits.set(limits);
        this._gravities.set(gravities);
        this._status.set('ready');
      },
      error: () => this._status.set('failed'),
    });
  }
}
