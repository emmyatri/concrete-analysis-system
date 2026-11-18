import { DOCUMENT, DatePipe, DecimalPipe, formatNumber } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
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

import { CuringChart } from '../curing-chart/curing-chart';
import { type CuringMeasurement } from '../curing-chart/curing-chart-config';
import { StrengthChart, type StrengthLoad } from '../strength-chart/strength-chart';

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
  imports: [RouterLink, DatePipe, DecimalPipe, StrengthChart, CuringChart],
  templateUrl: './batch-report.html',
  styleUrls: ['./batch-report.scss', './batch-report-print.scss'],
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
          map(({ batch, strength }): ReportState => ({
            status: 'loaded',
            batch,
            strength,
          })),
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

  // ---------- Warnings beside their values ----------

  /**
   * Batch, recipe and cement chemistry warnings by limit key, so each flagged value can show its
   * message where it appears. They are also listed under Warnings and Recommendations.
   */
  private readonly fieldWarnings = computed(() => {
    const messages = new Map<string, string>();
    for (const warning of this.batch()?.warnings ?? []) {
      if (
        ['Batch', 'Composition', 'CementOxideAnalysis'].includes(warning.entityType) &&
        !messages.has(warning.limitKey)
      ) {
        messages.set(warning.limitKey, warning.message);
      }
    }
    return messages;
  });

  /** Break warnings by break result id. */
  private readonly breakWarnings = computed(() => {
    const messages = new Map<number, string>();
    for (const warning of this.batch()?.warnings ?? []) {
      if (warning.entityType === 'BreakResult' && !messages.has(warning.entityId)) {
        messages.set(warning.entityId, warning.message);
      }
    }
    return messages;
  });

  /** The warning saved for a batch, recipe or cement chemistry value, if any. */
  protected fieldWarning(limitKey: string): string | null {
    return this.fieldWarnings().get(limitKey) ?? null;
  }

  /** The warning saved with a break result, if any. */
  protected breakWarning(breakResultId: number): string | null {
    return this.breakWarnings().get(breakResultId) ?? null;
  }

  // ---------- Curing conditions ----------

  /** One chart per measurement, in the order Batch Detail lists them. */
  protected readonly curingMeasurements: readonly CuringMeasurement[] = [
    'internalTempF',
    'ambientTempF',
    'moisturePct',
    'relativeHumidityPct',
    'resistivityOhmPerFt',
    'co2Ppm',
  ];

  /** Every curing reading with its cylinder label, as the curing chart expects. */
  protected readonly curingReadings = computed(() =>
    (this.batch()?.specimens ?? []).flatMap((specimen) =>
      specimen.curingReadings.map((reading) => ({ label: specimen.label, reading })),
    ),
  );

  /** Recorded validation warnings with readable report labels. */
  protected readonly warningRows = computed(() => {
    const batch = this.batch();
    if (!batch) return [];

    return batch.warnings.map((warning) => ({
      warning,
      record: this.describeWarningRecord(batch, warning),
      field: this.warningFieldLabel(warning),
      value: formatNumber(warning.value, 'en-US', this.warningDigits(warning.limitKey)),
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

  private describeWarningRecord(batch: BatchDetail, warning: SavedWarning): string {
    switch (warning.entityType) {
      case 'Batch':
        return 'Batch';
      case 'Composition':
        return 'Recipe';
      case 'CementOxideAnalysis':
        return 'Cement chemistry';
      case 'BreakResult': {
        const specimen = batch.specimens.find((s) => s.breakResult?.id === warning.entityId);
        return specimen ? `Cylinder ${specimen.label} · break` : 'Break';
      }
      case 'CuringReading': {
        const specimen = batch.specimens.find((s) =>
          s.curingReadings.some((r) => r.id === warning.entityId),
        );
        return specimen ? `Cylinder ${specimen.label} · curing reading` : 'Curing reading';
      }
      case 'Specimen': {
        const specimen = batch.specimens.find((s) => s.id === warning.entityId);
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
    const cssTitle = JSON.stringify(title).replace(/</g, '\\3C ').replace(/>/g, '\\3E ');

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

      /* Page 1 has the report's own title block, so no running title there. */
      @page :first {
        @top-right {
          content: none;
          border-bottom: none;
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

  /**
   * The API sends UTC times without a "Z", so the browser would read them as local time and
   * the report's UTC display would be off by the local offset. Mark them as UTC first.
   */
  protected utc(value: string): string {
    return /(Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`;
  }

  /** Number of cylinders with recorded compressive strength tests. */
  protected recordedBreakCount(specimens: Specimen[]): number {
    return specimens.filter((specimen) => specimen.breakResult !== null).length;
  }

  // ---------- Report actions ----------

  /**
   * Opens the print dialog through WebView2.
   * Falls back to browser printing during development.
   */
  protected printReport(): void {
    const webview = this.getWebView();

    if (webview) {
      webview.postMessage({ type: 'print-report' });
      return;
    }

    window.print();
  }

  protected saveReportPdf(): void {
    const webview = this.getWebView();

    if (!webview) {
      return;
    }

    webview.postMessage({
      type: 'save-report-pdf',
      batchNumber: this.batchNumber(),
    });
  }

  /**
   * Requests the batch's data as one Excel workbook, a tab per table.
   * WPF handles the native Save dialog and local API download.
   */
  protected exportBatchData(): void {
    const webview = this.getWebView();

    if (!webview) {
      return;
    }

    webview.postMessage({
      type: 'save-batch-data',
      batchNumber: this.batchNumber(),
    });
  }

  /**
   * SCRUM-214: saves the PDF and the data export to the cloud folder chosen in Settings.
   * WPF writes the files and explains when no folder is set or it can't be found.
   */
  protected saveToCloud(): void {
    const webview = this.getWebView();

    if (!webview) {
      return;
    }

    webview.postMessage({
      type: 'save-to-cloud',
      batchNumber: this.batchNumber(),
    });
  }

  protected readonly isDesktopHost = !!this.getWebView();

  private getWebView(): { postMessage: (message: unknown) => void } | undefined {
    return (
      window as Window & {
        chrome?: {
          webview?: {
            postMessage: (message: unknown) => void;
          };
        };
      }
    ).chrome?.webview;
  }

  protected retry(): void {
    this.attempt.update((value) => value + 1);
  }
}
