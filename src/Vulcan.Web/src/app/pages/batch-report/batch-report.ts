import { DOCUMENT, DatePipe, DecimalPipe, formatNumber } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of, startWith, switchMap } from 'rxjs';

import { describeApiError } from '../../core/api/api-errors';
import { earlyWarningsShortfallFirst } from '../../core/api/early-warnings';
import {
  BatchDetail,
  BatchStrength,
  EarlyWarning,
  SavedWarning,
  Specimen,
} from '../../core/api/models';
import { VulcanApiService } from '../../core/api/vulcan-api.service';

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

  // ---------- Early-strength warnings ----------

  /**
   * Orders early-strength warning reasons consistently with Batch Detail:
   * strength shortfalls first, followed by mix and curing factors.
   */
  protected readonly earlyWarningReasons = computed((): EarlyWarning[] => {
    const strength = this.strength();

    if (!strength) {
      return [];
    }

    return earlyWarningsShortfallFirst(strength.earlyWarnings);
  });

  /**
   * Uses the same risk assessment title as Batch Detail.
   */
  protected readonly earlyWarningTitle = computed(() => {
    const strength = this.strength();

    return strength?.atRisk
      ? `${strength.batchNumber} is at risk of missing its target`
      : 'Early warning';
  });


  /** Recorded validation warnings with readable report labels. */
  protected readonly warningRows = computed(() => {
    const batch = this.batch();
    if (!batch) return [];

    return batch.warnings.map((warning) => ({
      warning,
      record: this.describeWarningRecord(batch, warning),
      field: this.warningFieldLabel(warning),
      value: formatNumber(
        warning.value,
        'en-US',
        this.warningDigits(warning.limitKey),
      ),
    }));
  });

  private warningFieldLabel(warning: SavedWarning): string {
    const labels: Record<string, string> = {
      'Batch.FinalWeightLb': 'Sample weight (lb)',
      'Batch.MixWaterPh': 'Mix water pH',
      'Batch.CementSurfacePh': 'Cement surface pH',
      'Composition.WaterCementRatio': 'Water / cementitious',
      'Oxide.AluminaPct': 'Alumina Al₂O₃ (%)',
      'Oxide.IronOxidePct': 'Iron oxide Fe₂O₃ (%)',
      'Oxide.MagnesiumOxidePct': 'Magnesium oxide MgO (%)',
      'Oxide.SulfurTrioxidePct': 'Sulfur trioxide SO₃ (%)',
      'Break.StrengthPsi': 'Break strength (PSI)',
      'Curing.AmbientTempF': 'Ambient temp (°F)',
      'Curing.MoisturePct': 'Moisture (% of sample weight)',
    };

    return labels[warning.limitKey] ?? warning.fieldName;
  }

  private warningDigits(limitKey: string): string {
    switch (limitKey) {
      case 'Batch.MixWaterPh':
      case 'Batch.CementSurfacePh':
        return '1.1-2';

      case 'Curing.MoisturePct':
      case 'Curing.AmbientTempF':
        return '1.1-1';

      case 'Composition.WaterCementRatio':
        return '1.2-3';

      case 'Break.StrengthPsi':
        return '1.0-0';

      default:
        return '1.0-2';
    }
  }

  private describeWarningRecord(
    batch: BatchDetail,
    warning: SavedWarning,
  ): string {
    switch (warning.entityType) {
      case 'Batch':
        return 'Batch';
      case 'Composition':
        return 'Recipe';
      case 'CementOxideAnalysis':
        return 'Cement chemistry';
      case 'BreakResult': {
        const specimen = batch.specimens.find(
          (s) => s.breakResult?.id === warning.entityId,
        );
        return specimen ? `Cylinder ${specimen.label} · break` : 'Break';
      }
      case 'CuringReading': {
        const specimen = batch.specimens.find((s) =>
          s.curingReadings.some((r) => r.id === warning.entityId),
        );
        return specimen
          ? `Cylinder ${specimen.label} · curing reading`
          : 'Curing reading';
      }
      case 'Specimen': {
        const specimen = batch.specimens.find(
          (s) => s.id === warning.entityId,
        );
        return specimen ? `Cylinder ${specimen.label}` : 'Cylinder';
      }
    }
  }

  // ---------- Strength chart ----------

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

  // ---------- Prediction calibration ----------

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

  // ---------- Print configuration ----------

  /**
   * Configures the running print header and synthetic-data footer.
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
          content: ${batch.isSynthetic ? '"SYNTHETIC TEST DATA"' : 'none'};
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

  // ---------- Report metadata ----------

  protected readonly generatedAt = new Date();

  /** Number of cylinders with recorded compressive strength tests. */
  protected recordedBreakCount(specimens: Specimen[]): number {
    return specimens.filter(
      (specimen) => specimen.breakResult !== null,
    ).length;
  }

  // ---------- Report actions ----------

  protected printReport(): void {
    window.print();
  }

  protected retry(): void {
    this.attempt.update((value) => value + 1);
  }
}
