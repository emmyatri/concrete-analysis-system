import { DatePipe, DecimalPipe, formatNumber } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { catchError, map, of, startWith, switchMap } from 'rxjs';

import { describeApiError } from '../../core/api/api-errors';
import { earlyWarningsShortfallFirst } from '../../core/api/early-warnings';
import { BatchDetail, EarlyWarning, SavedWarning, Specimen } from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { AddCylinderDialog } from '../add-cylinder-dialog/add-cylinder-dialog';
import { CuringSummary } from '../curing-summary/curing-summary';
import { EstimateCards } from '../estimate-cards/estimate-cards';
import { StrengthChart, StrengthLoad } from '../strength-chart/strength-chart';
import { CuringChart } from '../curing-chart/curing-chart';
import type { CuringMeasurement } from '../curing-chart/curing-chart-config';

type LoadState =
  | { status: 'loading' }
  | { status: 'loaded'; batch: BatchDetail }
  | { status: 'not-found' }
  | { status: 'failed'; message: string };

const CEMENT_LABELS: Record<BatchDetail['cementType'], string> = {
  TypeI: 'Type I',
  TypeII: 'Type II',
  TypeIII: 'Type III',
};

const SCM_LABELS: Record<BatchDetail['composition']['scmType'], string> = {
  None: 'None',
  FlyAsh: 'Fly ash',
  Slag: 'Slag',
  SilicaFume: 'Silica fume',
};

const TEST_AGE_LABELS: Record<Specimen['testAgeDays'], string> = {
  Day3: 'Day 3',
  Day7: 'Day 7',
  Day28: 'Day 28',
};

/** How each flagged field is named and how many decimals its value shows. */
const FIELDS: Record<string, { label: string; short?: string; digits: string }> = {
  'Batch.FinalWeightLb': { label: 'Sample weight (lb)', digits: '1.0-2' },
  'Batch.MixWaterPh': { label: 'Mix water pH', digits: '1.1-2' },
  'Batch.CementSurfacePh': { label: 'Cement surface pH', digits: '1.1-2' },
  'Composition.WaterCementRatio': { label: 'Water / cementitious', digits: '1.2-3' },
  'Oxide.AluminaPct': { label: 'Alumina Al₂O₃ (%)', short: 'Al₂O₃', digits: '1.2-2' },
  'Oxide.IronOxidePct': { label: 'Iron oxide Fe₂O₃ (%)', short: 'Fe₂O₃', digits: '1.2-2' },
  'Oxide.MagnesiumOxidePct': { label: 'Magnesium oxide MgO (%)', short: 'MgO', digits: '1.2-2' },
  'Oxide.SulfurTrioxidePct': { label: 'Sulfur trioxide SO₃ (%)', short: 'SO₃', digits: '1.2-2' },
  'Break.StrengthPsi': { label: 'Break strength (PSI)', digits: '1.0-0' },
  'Curing.AmbientTempF': { label: 'Ambient temp (°F)', digits: '1.1-1' },
  'Curing.MoisturePct': { label: 'Moisture (% of sample weight)', digits: '1.1-1' },
};

/**
 * Everything about one batch (M1-50, mockup "08 · Batch Detail"): summary, recipe, chemistry, cylinders,
 * breaks, curing readings and saved warnings. Opened from Search and Home by batch number.
 */
@Component({
  selector: 'app-batch-detail',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    ButtonModule,
    DialogModule,
    TableModule,
    TabsModule,
    AddCylinderDialog,
    StrengthChart,
    CuringChart,
    EstimateCards,
    CuringSummary,
  ],
  templateUrl: './batch-detail.html',
  styleUrl: './batch-detail.scss',
})
export class BatchDetailPage {
  private readonly api = inject(VulcanApiService);
  private readonly referenceData = inject(ReferenceDataService);
  private readonly router = inject(Router);

  /** From the route, /batches/:batchNumber. */
  readonly batchNumber = input.required<string>();

  /** ?addCylinder=1 opens Add cylinder straight away (New batch's "Save & add cylinders"). */
  readonly addCylinder = input<string>();

  /** Bumped by "Try again" to load the same batch again. */
  private readonly attempt = signal(0);

  /** True for a reload after adding cylinders, so the page stays on screen instead of showing "Loading". */
  private quietReload = false;

  protected readonly addCylinderOpen = signal(false);
  /** Set when a cylinder saves, so the batch reloads once the dialog closes. */
  private cylindersAdded = false;

  protected readonly state = toSignal(
    toObservable(
      computed(() => ({ batchNumber: this.batchNumber(), attempt: this.attempt() })),
    ).pipe(
      switchMap(({ batchNumber }) => {
        const quiet = this.quietReload;
        this.quietReload = false;
        const load = this.api.getBatch(batchNumber).pipe(
          map((batch): LoadState => ({ status: 'loaded', batch })),
          catchError((error: unknown) =>
            of<LoadState>(
              error instanceof HttpErrorResponse && error.status === 404
                ? { status: 'not-found' }
                : { status: 'failed', message: describeApiError(error).message },
            ),
          ),
        );
        return quiet ? load : load.pipe(startWith<LoadState>({ status: 'loading' }));
      }),
    ),
    { initialValue: { status: 'loading' } as LoadState },
  );

  protected readonly batch = computed(() => {
    const state = this.state();
    return state.status === 'loaded' ? state.batch : null;
  });

  protected readonly failure = computed(() => {
    const state = this.state();
    return state.status === 'failed' ? state.message : null;
  });

  protected readonly cementLabels = CEMENT_LABELS;

  // ---------- Strength chart ----------

  /** Which batch the chart last loaded, so a reload after adding cylinders doesn't blank the chart. */
  private strengthBatchNumber: string | null = null;

  /** Loaded next to the batch. If it fails, the rest of the page still shows. */
  protected readonly strengthState = toSignal(
    toObservable(
      computed(() => ({ batchNumber: this.batchNumber(), attempt: this.attempt() })),
    ).pipe(
      switchMap(({ batchNumber }) => {
        const sameBatch = this.strengthBatchNumber === batchNumber;
        this.strengthBatchNumber = batchNumber;
        const load = this.api.getStrength(batchNumber).pipe(
          map((strength): StrengthLoad => ({ status: 'loaded', strength })),
          catchError((error: unknown) =>
            of<StrengthLoad>({ status: 'failed', message: describeApiError(error).message }),
          ),
        );
        return sameBatch ? load : load.pipe(startWith<StrengthLoad>({ status: 'loading' }));
      }),
    ),
    { initialValue: { status: 'loading' } as StrengthLoad },
  );

  /** Plain-language reasons from GET strength, shortfall first. Empty when the batch is on track. */
  protected readonly earlyWarningReasons = computed((): EarlyWarning[] => {
    const state = this.strengthState();
    if (state.status !== 'loaded') return [];
    return earlyWarningsShortfallFirst(state.strength.earlyWarnings);
  });

  /** "VM-SYN-006 is at risk of missing its target" when the prediction says so (mockup M2-01). */
  protected readonly earlyWarningTitle = computed(() => {
    const state = this.strengthState();
    return state.status === 'loaded' && state.strength.atRisk
      ? `${state.strength.batchNumber} is at risk of missing its target`
      : 'Early warning';
  });

  /**
   * The chart's minimum line: Prediction.MinimumPsi once SCRUM-181 adds it, until then the break
   * strength Warn limit (both 3,000 PSI, the client's minimum).
   */
  protected readonly minimumPsi = computed(
    () =>
      this.referenceData.limitFor('Prediction.MinimumPsi')?.warn?.min ??
      this.referenceData.limitFor('Break.StrengthPsi')?.warn?.min ??
      null,
  );

  /** Cylinders whose break saved with a warning, drawn in orange on the chart. */
  protected readonly flaggedSpecimenIds = computed(
    () =>
      new Set(
        (this.batch()?.specimens ?? [])
          .filter(
            (s) =>
              s.breakResult &&
              this.warningFor('BreakResult', s.breakResult.id, 'Break.StrengthPsi'),
          )
          .map((s) => s.id),
      ),
  );

  // ---------- Curing chart ----------

  protected readonly curingPoints = computed(() => {
    const state = this.strengthState();
    if (state.status === 'loaded') {
      return state.strength.curingPoints;
    }
    return [];
  });

  // ---------- Shared chart interaction ----------

  /** Days hovered over in either strength or curing charts are highlighted in both charts (M2-44). */
  protected readonly hoveredDay = signal<number | null>(null);
  /** The strength chart's larger pop-out view. */
  protected readonly chartExpanded = signal(false);
  /** The curing strip's larger pop-out view, on this measurement; null when closed. */
  protected readonly curingExpanded = signal<CuringMeasurement | null>(null);

  // ---------- Saved warnings ----------

  /** The saved warning for one value, if it was flagged. */
  protected warningFor(entityType: SavedWarning['entityType'], entityId: number, limitKey: string) {
    return this.batch()?.warnings.find(
      (w) => w.entityType === entityType && w.entityId === entityId && w.limitKey === limitKey,
    );
  }

  protected readonly phWarning = computed(() => {
    const batch = this.batch();
    return batch ? this.warningFor('Batch', batch.id, 'Batch.MixWaterPh') : undefined;
  });

  protected readonly surfacePhWarning = computed(() => {
    const batch = this.batch();
    return batch ? this.warningFor('Batch', batch.id, 'Batch.CementSurfacePh') : undefined;
  });

  protected readonly weightWarning = computed(() => {
    const batch = this.batch();
    return batch ? this.warningFor('Batch', batch.id, 'Batch.FinalWeightLb') : undefined;
  });

  protected readonly ratioWarning = computed(() => {
    const batch = this.batch();
    return batch
      ? this.warningFor('Composition', batch.id, 'Composition.WaterCementRatio')
      : undefined;
  });

  // ---------- Recipe ----------

  /** Bar segments and legend rows, in the mockup's order. Air / other is last. */
  protected readonly mix = computed(() => {
    const c = this.batch()?.composition;
    if (!c) return [];
    return [
      { label: 'Cement', cls: 'mix-cement', pct: c.cementPct },
      { label: 'Water', cls: 'mix-water', pct: c.waterPct },
      { label: 'Coarse agg.', cls: 'mix-coarse', pct: c.coarseAggregatePct },
      { label: 'Fine agg.', cls: 'mix-fine', pct: c.fineAggregatePct },
      { label: `SCM (${SCM_LABELS[c.scmType].toLowerCase()})`, cls: 'mix-scm', pct: c.scmPct },
      { label: 'Air / other', cls: 'mix-air', pct: Math.max(0, c.airOtherPct) },
    ];
  });

  // ---------- Chemistry ----------

  protected readonly oxides = computed(() => {
    const batch = this.batch();
    if (!batch) return [];
    const row = (key: string, value: number) => ({
      key,
      label: FIELDS[key].short ?? FIELDS[key].label,
      value,
      normal: this.normalRange(key),
      warning: this.warningFor('CementOxideAnalysis', batch.id, key),
    });
    return [
      row('Oxide.AluminaPct', batch.oxides.aluminaPct),
      row('Oxide.IronOxidePct', batch.oxides.ironOxidePct),
      row('Oxide.MagnesiumOxidePct', batch.oxides.magnesiumOxidePct),
      row('Oxide.SulfurTrioxidePct', batch.oxides.sulfurTrioxidePct),
    ];
  });

  /** The oxides with a saved warning, for the line under the values. */
  protected readonly flaggedOxides = computed(() => this.oxides().filter((o) => o.warning));

  /** "3.0–7.5" from the Warn limit, or empty until the limits have loaded. */
  private normalRange(key: string): string {
    const warn = this.referenceData.limitFor(key)?.warn;
    if (!warn || warn.min === null || warn.max === null) return '';
    return `${formatNumber(warn.min, 'en-US', '1.1-2')}–${formatNumber(warn.max, 'en-US', '1.1-2')}`;
  }

  // ---------- Tabs ----------

  protected readonly cylinders = computed(() => {
    const batch = this.batch();
    if (!batch) return [];
    return batch.specimens.map((specimen) => ({
      specimen,
      testAge: TEST_AGE_LABELS[specimen.testAgeDays],
      breakWarning: specimen.breakResult
        ? this.warningFor('BreakResult', specimen.breakResult.id, 'Break.StrengthPsi')
        : undefined,
    }));
  });

  /**
   * Every reading with its cylinder's label, grouped by cylinder in the table. The table sorts by the
   * group field, so it groups by the cylinder's position (not its label) to keep D3 before D28.
   */
  protected readonly readings = computed(() =>
    (this.batch()?.specimens ?? []).flatMap((specimen, position) =>
      specimen.curingReadings.map((reading) => ({
        position,
        label: specimen.label,
        reading,
        ambientWarning: this.warningFor('CuringReading', reading.id, 'Curing.AmbientTempF'),
      })),
    ),
  );

  protected readingCount(label: string): number {
    return this.batch()?.specimens.find((s) => s.label === label)?.curingReadings.length ?? 0;
  }

  /** The Warnings tab: which record, which field, the value and the message. */
  protected readonly warningRows = computed(() => {
    const batch = this.batch();
    if (!batch) return [];
    return batch.warnings.map((warning) => ({
      warning,
      record: this.describeRecord(batch, warning),
      field: FIELDS[warning.limitKey]?.label ?? warning.fieldName,
      value: formatNumber(warning.value, 'en-US', FIELDS[warning.limitKey]?.digits ?? '1.0-2'),
    }));
  });

  private describeRecord(batch: BatchDetail, warning: SavedWarning): string {
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

  protected retry(): void {
    this.attempt.update((n) => n + 1);
  }

  constructor() {
    // Opened with ?addCylinder=1: show the dialog once the batch is on screen, then drop the flag from the
    // address so going back or reloading doesn't open it again.
    effect(() => {
      if (this.addCylinder() !== '1' || !this.batch()) return;
      untracked(() => {
        this.addCylinderOpen.set(true);
        this.router.navigate([], {
          queryParams: { addCylinder: null },
          queryParamsHandling: 'merge',
          replaceUrl: true,
        });
      });
    });
  }

  protected cylinderSaved(): void {
    this.cylindersAdded = true;
  }

  /** The dialog closed: reload the batch if anything was added, so the new cylinders show. */
  protected addCylinderClosed(open: boolean): void {
    this.addCylinderOpen.set(open);
    if (open || !this.cylindersAdded) return;
    this.cylindersAdded = false;
    this.quietReload = true;
    this.attempt.update((n) => n + 1);
  }
}
