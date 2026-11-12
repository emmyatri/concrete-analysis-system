
import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of, startWith, switchMap } from 'rxjs';

import { describeApiError } from '../../core/api/api-errors';
import { BatchDetail, BatchStrength, Specimen } from '../../core/api/models';
import { VulcanApiService } from '../../core/api/vulcan-api.service';

import { DatePipe, DecimalPipe } from '@angular/common';

import {
  StrengthChart,
  type StrengthLoad,
} from '../strength-chart/strength-chart';


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
  imports: [RouterLink, DatePipe, DecimalPipe, StrengthChart],
  templateUrl: './batch-report.html',
  styleUrl: './batch-report.scss',
})
export class BatchReport {
  private readonly api = inject(VulcanApiService);

  private readonly document = inject(DOCUMENT);

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

  /**
   * Adapts the report's existing strength response
   * for the reusable StrengthChart component.
   */
  protected readonly strengthChartState = computed<StrengthLoad>(() => {
    const state = this.state();

    switch (state.status) {
      case 'loaded':
        return {
          status: 'loaded',
          strength: state.strength,
        };

      case 'failed':
        return {
          status: 'failed',
          message: state.message,
        };

      case 'not-found':
        return {
          status: 'failed',
          message: 'Batch report not found.',
        };

      default:
        return { status: 'loading' };
    }
  });

  /**
   * Groups estimates that share the same calculation basis
   * and calibration version.
   *
   * If different estimates use different methods or versions,
   * each combination remains visible in the report.
   */
  protected readonly calibrationDetails = computed(() => {
    const estimates = this.strength()?.estimates ?? [];

    const groups = new Map<
      string,
      {
        basis: string;
        version: string;
        horizons: number[];
      }
    >();

    for (const estimate of estimates) {
      const basis = estimate.basis;
      const version = estimate.calibratedVersion;

      const key = JSON.stringify([basis, version]);
      const existing = groups.get(key);

      if (existing) {
        existing.horizons.push(estimate.horizonDays);
      } else {
        groups.set(key, {
          basis,
          version,
          horizons: [estimate.horizonDays],
        });
      }
    }

    return [...groups.values()];
  });

  /** Converts prediction adjustment codes into report-friendly labels. */
  protected adjustmentLabel(stage: string | null): string {
    switch (stage) {
      case 'ThreeAndSevenDay':
        return '3- and 7-day break adjustment';
      case 'SingleBreak':
        return 'Single-break adjustment';
      case null:
      case '':
        return 'Not specified';
      default:
        return stage;
    }
  }

  /**
   * Configures the running print header and synthetic-data footer
   */
  private readonly printHeaderEffect = effect((onCleanup) => {
    const state = this.state();

    if (state.status !== 'loaded') {
      return;
    }

    const batch = state.batch;
    const title = `${batch.batchNumber} Batch Report`;

    // Safely serialize the dynamic title as a CSS string.
    const cssTitle = JSON.stringify(title)
      .replace(/</g, '\\3C ')
      .replace(/>/g, '\\3E ');

    const style = this.document.createElement('style');
    style.setAttribute('data-batch-report-print-header', '');

    style.textContent = `
    @page {
      @top-right {
        content: ${cssTitle};
      }

      @bottom-center {
        content: ${batch.isSynthetic
      ? '"SYNTHETIC TEST DATA"'
      : 'none'};
        color: #9b3434;
        font-family: Arial, Helvetica, sans-serif;
        font-size: 8pt;
        font-weight: 700;
        letter-spacing: 0.4px;
        border-top: 1px solid #cbd5e1;
      }
    }
  `;

    this.document.head.appendChild(style);

    onCleanup(() => {
      style.remove();
    });
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
