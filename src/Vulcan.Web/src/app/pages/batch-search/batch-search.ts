import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';

import { describeApiError } from '../../core/api/api-errors';
import { CEMENT_LABELS } from '../../core/api/cement-labels';
import { BatchQuery, BatchSummary } from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { SyntheticDataSetting } from '../../core/session/synthetic-data';
import { ValidationService } from '../../core/validation/validation.service';

type Results =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'loaded'; batches: BatchSummary[]; search: string }
  | { status: 'failed'; message: string };

const CEMENT_OPTIONS = (Object.keys(CEMENT_LABELS) as BatchSummary['cementType'][]).map(
  (value) => ({
    label: CEMENT_LABELS[value],
    value,
  }),
);

/** "2026-10-05" in local time, which is how the server compares cast dates. */
function isoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/**
 * Find a batch by any part of its number (M1-64, mockup "07 · Search batches"), with optional filters.
 * Follows the Synthetic data switch. Clicking a result opens Batch Detail.
 */
@Component({
  selector: 'app-batch-search',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    InputTextModule,
    SelectModule,
    TableModule,
  ],
  templateUrl: './batch-search.html',
  styleUrl: './batch-search.scss',
})
export class BatchSearchPage {
  private readonly api = inject(VulcanApiService);
  private readonly router = inject(Router);
  private readonly referenceData = inject(ReferenceDataService);
  private readonly validation = inject(ValidationService);
  private readonly synthetic = inject(SyntheticDataSetting);

  protected cementLabel(type: BatchSummary['cementType']): string {
    return CEMENT_LABELS[type] ?? type;
  }
  protected readonly cementOptions = CEMENT_OPTIONS;

  protected readonly form = inject(NonNullableFormBuilder).group({
    search: [''],
    castRange: new FormControl<Date[] | null>(null),
    cementType: new FormControl<BatchSummary['cementType'] | null>(null),
    hasWarnings: [false],
  });

  /** The last search run, or null before the first one (or after an empty one). */
  private readonly query = signal<BatchQuery | null>(null);

  protected readonly results = toSignal(
    combineLatest([toObservable(this.query), toObservable(this.synthetic.show)]).pipe(
      switchMap(([query, includeSynthetic]) => {
        if (!query) return of<Results>({ status: 'idle' });
        return this.api.getBatches({ ...query, includeSynthetic }).pipe(
          map((batches): Results => ({ status: 'loaded', batches, search: query.search ?? '' })),
          catchError((error: unknown) =>
            of<Results>({ status: 'failed', message: describeApiError(error).message }),
          ),
          startWith<Results>({ status: 'loading' }),
        );
      }),
    ),
    { initialValue: { status: 'idle' } as Results },
  );

  protected readonly batches = computed(() => {
    const results = this.results();
    return results.status === 'loaded' ? results.batches : [];
  });

  /** What to say instead of a table: nothing typed yet, still loading, nothing found, or an error. */
  protected readonly message = computed(() => {
    const results = this.results();
    switch (results.status) {
      case 'idle':
        return 'Type a batch number to search';
      case 'loading':
        return 'Searching…';
      case 'failed':
        return results.message;
      case 'loaded':
        if (results.batches.length > 0) return '';
        return results.search
          ? `No batches match '${results.search}'`
          : 'No batches match these filters';
    }
  });

  /** Runs the search. With nothing typed and no filters, shows the "Type a batch number" hint instead. */
  protected run(): void {
    const { search, castRange, cementType, hasWarnings } = this.form.getRawValue();
    const query: BatchQuery = {};
    if (search.trim()) query.search = search.trim();
    if (castRange?.[0]) query.castFrom = isoDate(castRange[0]);
    if (castRange?.[1]) query.castTo = isoDate(castRange[1]);
    if (cementType) query.cementType = cementType;
    if (hasWarnings) query.hasWarnings = true;
    this.query.set(Object.keys(query).length > 0 ? query : null);
  }

  protected clear(): void {
    this.form.reset();
    this.query.set(null);
  }

  protected open(batch: BatchSummary): void {
    this.router.navigate(['/batches', batch.batchNumber]);
  }

  /** Amber, as on the other screens, when a value is outside its normal (Warn) range. */
  protected isFlagged(limitKey: string, value: number | null): boolean {
    if (value === null) return false;
    return this.validation.evaluate(limitKey, value, this.referenceData.limits()).severity !== null;
  }
}
