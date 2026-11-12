
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of, startWith, switchMap } from 'rxjs';

import { describeApiError } from '../../core/api/api-errors';
import { BatchDetail, BatchStrength, Specimen } from '../../core/api/models';
import { VulcanApiService } from '../../core/api/vulcan-api.service';

import { DatePipe, DecimalPipe } from '@angular/common';

type ReportState =
  | { status: 'loading' }
  | {
  status: 'loaded';
  batch: BatchDetail;
  strength: BatchStrength;
}
  | { status: 'not-found' }
  | { status: 'failed'; message: string };

@Component({
  selector: 'app-batch-report',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './batch-report.html',
  styleUrl: './batch-report.scss',
})
export class BatchReport {
  private readonly api = inject(VulcanApiService);

  /** Batch number supplied by /batches/:batchNumber/report. */
  readonly batchNumber = input.required<string>();

  /** Allows the user to retry a failed request. */
  private readonly attempt = signal(0);

  /** Loads the batch record and strength data together. */
  protected readonly state = toSignal(
    toObservable(
      computed(() => ({
        batchNumber: this.batchNumber(),
        attempt: this.attempt(),
      })),
    ).pipe(
      switchMap(({ batchNumber }) =>
        forkJoin({
          batch: this.api.getBatch(batchNumber),
          strength: this.api.getStrength(batchNumber),
        }).pipe(
          map(
            ({ batch, strength }): ReportState => ({
              status: 'loaded',
              batch,
              strength,
            }),
          ),
          catchError((error: unknown) =>
            of<ReportState>(
              error instanceof HttpErrorResponse && error.status === 404
                ? { status: 'not-found' }
                : {
                  status: 'failed',
                  message: describeApiError(error).message,
                },
            ),
          ),
          startWith<ReportState>({ status: 'loading' }),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as ReportState },
  );

  protected readonly batch = computed(() => {
    const state = this.state();
    return state.status === 'loaded' ? state.batch : null;
  });

  protected readonly strength = computed(() => {
    const state = this.state();
    return state.status === 'loaded' ? state.strength : null;
  });

  protected readonly generatedAt = new Date();


  /** Number of cylinders with recorded compressive strength tests. */
  protected recordedBreakCount(specimens: Specimen[]): number {
    return specimens.filter(
      (specimen) => specimen.breakResult !== null,
    ).length;
  }

  protected printReport(): void {
    window.print();
  }

  protected retry(): void {
    this.attempt.update((value) => value + 1);
  }
}
