import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';

import { describeApiError } from '../../core/api/api-errors';
import { PendingBreak } from '../../core/api/models';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { SyntheticDataSetting } from '../../core/session/synthetic-data';
import { BreakDialog, BreakSaved, TEST_AGE_DAYS, ageInDays } from '../break-dialog/break-dialog';

type Pending =
  | { status: 'loading' }
  | { status: 'loaded'; cylinders: PendingBreak[] }
  | { status: 'failed'; message: string };

/**
 * Cylinders waiting to be crushed (M1-62, mockup "09 · Break results"), soonest due first, with a
 * Record break button on each. Opened from Home, or from Batch Detail with ?batch= (and &cylinder=
 * to open the dialog for one cylinder straight away).
 */
@Component({
  selector: 'app-break-results',
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    TableModule,
    BreakDialog,
  ],
  templateUrl: './break-results.html',
  styleUrl: './break-results.scss',
})
export class BreakResultsPage {
  private readonly api = inject(VulcanApiService);
  private readonly synthetic = inject(SyntheticDataSetting);

  /** ?batch=VM-2026-0142 fills in the filter. */
  readonly batch = input<string>();
  /** ?cylinder=A1 (with ?batch) opens the dialog for that cylinder once the list loads. */
  readonly cylinder = input<string>();

  protected readonly filter = signal('');
  private readonly reload = signal(0);

  protected readonly pending = toSignal(
    combineLatest([toObservable(this.synthetic.show), toObservable(this.reload)]).pipe(
      switchMap(([includeSynthetic]) =>
        this.api.getPendingBreaks(includeSynthetic).pipe(
          map((cylinders): Pending => ({ status: 'loaded', cylinders })),
          catchError((error: unknown) =>
            of<Pending>({ status: 'failed', message: describeApiError(error).message }),
          ),
          startWith<Pending>({ status: 'loading' }),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as Pending },
  );

  /** The list, narrowed to batch numbers containing the filter, ignoring upper/lowercase. */
  protected readonly rows = computed(() => {
    const pending = this.pending();
    if (pending.status !== 'loaded') return [];
    const filter = this.filter().trim().toLowerCase();
    return pending.cylinders
      .filter((row) => !filter || row.batchNumber.toLowerCase().includes(filter))
      .map((row) => {
        const age = ageInDays(row.castAt);
        const testAge = TEST_AGE_DAYS[row.testAgeDays];
        return { ...row, age, testAge, due: age >= testAge };
      });
  });

  protected readonly message = computed(() => {
    const pending = this.pending();
    if (pending.status === 'loading') return 'Loading cylinders…';
    if (pending.status === 'failed') return pending.message;
    if (pending.cylinders.length === 0) return 'No cylinders are waiting to be broken.';
    if (this.rows().length === 0)
      return `No cylinders waiting on batches matching '${this.filter().trim()}'`;
    return '';
  });

  protected readonly dialogOpen = signal(false);
  protected readonly selected = signal<PendingBreak | null>(null);
  protected readonly lastSaved = signal<BreakSaved | null>(null);
  private openedFromLink = false;

  constructor() {
    effect(() => {
      const batch = this.batch();
      if (batch) this.filter.set(batch);
    });

    // Batch Detail's "Record break" on a cylinder: open its dialog once, as soon as it's in the list.
    effect(() => {
      const pending = this.pending();
      const label = this.cylinder()?.toLowerCase();
      const batch = this.batch()?.toLowerCase();
      if (this.openedFromLink || !label || !batch || pending.status !== 'loaded') return;
      this.openedFromLink = true;
      const match = pending.cylinders.find(
        (row) => row.batchNumber.toLowerCase() === batch && row.label.toLowerCase() === label,
      );
      if (match) this.record(match);
    });
  }

  protected record(cylinder: PendingBreak): void {
    this.selected.set(cylinder);
    this.lastSaved.set(null);
    this.dialogOpen.set(true);
  }

  protected breakSaved(result: BreakSaved): void {
    this.lastSaved.set(result);
    this.reload.update((n) => n + 1);
  }

  protected refresh(): void {
    this.reload.update((n) => n + 1);
  }
}
