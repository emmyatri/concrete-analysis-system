import { ChartModule, UIChart } from 'primeng/chart';
import { SelectModule } from 'primeng/select';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import {
  Component,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  output,
  viewChild,
} from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { CuringPoint, CuringReading, SavedWarning } from '../../core/api/models';
import {
  type CuringChartColors,
  curingChartConfig,
  type CuringMeasurement,
  measurementLabels,
} from './curing-chart-config';
import type { Plugin } from 'chart.js';
import { chartHoverPlugin } from '../../shared/chart-hover/chart-hover-plugin';

/** The ValidationLimit key that flags each measurement (schema.md section 4). */
const MEASUREMENT_LIMIT_KEYS: Record<CuringMeasurement, string> = {
  internalTempF: 'Curing.InternalTempF',
  ambientTempF: 'Curing.AmbientTempF',
  moisturePct: 'Curing.MoisturePct',
  relativeHumidityPct: 'Curing.RelativeHumidityPct',
  resistivityOhmPerFt: 'Curing.ResistivityOhmPerFt',
  co2Ppm: 'Curing.Co2Ppm',
};

/**
 * Curing reading over days since cast for one batch (SCRUM-176, FR-12, mockups M2-01 and M2-02).
 * Its own component so chart.js and its styles stay out of Batch Detail.
 */
@Component({
  selector: 'app-curing-chart',
  imports: [ChartModule, SelectModule, FormsModule, ButtonModule],
  templateUrl: './curing-chart.html',
  styleUrl: './curing-chart.scss',
})
export class CuringChart {
  private readonly document = inject(DOCUMENT);

  /** Curing readings for the batch, with each reading's cylinder label. */
  readonly readings = input.required<
    {
      label: string;
      reading: CuringReading;
    }[]
  >();

  /** Every saved warning for the batch; only curing-reading warnings are used here. */
  readonly warnings = input<SavedWarning[]>([]);

  /** Curing measurements for the batch, or an empty array until strength data loads. */
  readonly curingPoints = input.required<CuringPoint[]>();
  /** The batch's cast time (ISO, UTC), for "Latest reading: 18 h". */
  readonly castAt = input.required<string>();

  /** Chart height; the expanded view on Batch Detail makes it taller. */
  readonly height = input('120px');
  /** Text size for axes and tooltip; the expanded view makes it larger and shows axis titles. */
  readonly fontSize = input(12);
  /** Shows the Expand button. Off for the expanded copy itself. */
  readonly expandable = input(true);
  /** The measurement to show first; the expanded view opens on the one shown on the page. */
  readonly initialMeasurement = input<CuringMeasurement>('internalTempF');
  /**
   * The batch report's version: one fixed measurement, titled by it, with no selector, Expand or
   * hover, drawn at 3x so it stays sharp in the PDF.
   */
  readonly printMode = input(false);
  /** Asks Batch Detail to open the larger view, on the measurement shown now. */
  readonly expand = output<CuringMeasurement>();

  /** Day hovered over by either strength or curing charts. (M2-44) */
  readonly hoveredDay = input<number | null>(null);
  readonly hoveredDayChange = output<number | null>();
  private readonly chartWidget = viewChild(UIChart);

  /** The user's selected measurement for filtering the view of curing readings;
   * default = internal temp (M2-44).
   * */
  readonly selectedMeasurement = linkedSignal<CuringMeasurement>(() => this.initialMeasurement());
  readonly measurementOptions = Object.entries(measurementLabels).map(([value, label]) => ({
    value,
    label,
  }));

  /**
   * Readings saved with a warning for the selected measurement, keyed "label | time" like the
   * curing points. Matched by reading id and limit key, so every measurement with a Warn limit
   * is marked, not just ambient (SCRUM-176 step 3).
   */
  readonly flaggedReadings = computed(() => {
    const limitKey = MEASUREMENT_LIMIT_KEYS[this.selectedMeasurement()];
    const messages = new Map(
      this.warnings()
        .filter((w) => w.entityType === 'CuringReading' && w.limitKey === limitKey)
        .map((w) => [w.entityId, w.message] as const),
    );
    return new Map<string, string>(
      this.readings()
        .filter((row) => messages.has(row.reading.id))
        .map(
          (row) =>
            [
              `${row.label} | ${Date.parse(row.reading.takenAt)}`,
              messages.get(row.reading.id)!,
            ] as const,
        ),
    );
  });

  /** Caption inside the strip: "Line = average of 9 cylinders · band = lowest to highest ·
   * readings so far: 0–72 h" (use the real cylinder count and hours). (M2-44)
   * */
  protected readonly cylinderCount = computed(
    () => new Set(this.readings().map((s) => s.label)).size,
  );

  readonly readingCaption = computed(() => {
    const hours = this.chart().readingHours;
    if (hours.length === 0) {
      return 'No curing readings yet';
    }

    const cylinders = this.cylinderCount();
    const firstHour = Math.round(hours[0]);
    const lastHour = Math.round(hours[hours.length - 1]);
    /** Could have multiple readings across cylinders (9 cylinders x 2 readings).
     * Count based on hours in array not individual cylinders' readings.
     */
    if (hours.length === 1) {
      return `${hours.length} reading so far (${firstHour} h)`;
    }
    if (hours.length === 2) {
      return `${hours.length} readings so far (${firstHour} h and ${lastHour} h)`;
    }
    return `Line = average of ${cylinders} ${cylinders === 1 ? 'cylinder' : 'cylinders'} · band = lowest to highest · readings so far: ${firstHour}–${lastHour}h`;
  });

  /** The selected measurement's name, the chart's title on the report. */
  protected readonly measurementLabel = computed(
    () => measurementLabels[this.selectedMeasurement()],
  );

  /** Identifies the configuration pieces for the curing chart.*/
  protected readonly chart = computed(() =>
    curingChartConfig(
      this.curingPoints(),
      this.castAt(),
      this.colors(),
      this.selectedMeasurement(),
      this.flaggedReadings(),
      this.fontSize(),
    ),
  );

  /** The theme's colors (vulcan-preset.ts), so the chart follows the theme like the rest of the app. */
  private colors(): CuringChartColors {
    const style = this.document.defaultView?.getComputedStyle(this.document.documentElement);
    const token = (name: string, fallback: string) =>
      style?.getPropertyValue(name).trim() || fallback;
    return {
      text: token('--p-text-muted-color', '#696e79'),
      grid: token('--p-surface-200', '#dddfe4'),
      font: style?.getPropertyValue('font-family').trim() || 'Inter, sans-serif',
      line: token('--p-primary-500', '#263A78'),
      shade: token('--p-blue-200', '#BFDBFE'),
      flagged: token('--p-orange-500', '#F97316'),
    };
  }

  /** Chart options; the report's copy is static and drawn at 3x for print. */
  protected readonly options = computed(() => {
    const options = this.chart().options;
    if (!this.printMode()) return options;

    const scales = options.scales ?? {};
    return {
      ...options,
      devicePixelRatio: 3,
      animation: false as const,
      events: [],
      // Stands alone on the report, so no gutter to line up with the strength chart.
      layout: { padding: { top: 4, right: 8 } },
      scales: {
        ...scales,
        // Show the days under each chart, since the Day 1 / Day 3 labels are left off.
        x: {
          ...scales['x'],
          ticks: { ...scales['x']?.ticks, display: true },
          title: { ...scales['x']?.title, display: true, text: 'Days since cast' },
        },
        // Wide enough for labels like "440 ppm" and "100 Ω/ft" at the report's text size.
        y: {
          ...scales['y'],
          afterFit: (scale: { width: number }) => {
            scale.width = 64;
          },
        },
      },
      plugins: {
        ...options.plugins,
        tooltip: { ...options.plugins?.tooltip, enabled: false },
      },
    };
  });

  /** On the report, no labels or hover: the charts are small and the x-axis shows the days. */
  protected readonly plugins = computed<Plugin<'line'>[]>(() =>
    this.printMode() ? [] : [this.dayLabelsPlugin, this.hoverPlugin],
  );

  private readonly dayLabelsPlugin: Plugin<'line'> = {
    /** Faint vertical lines at Day 1 and Day 3, labeled "Day 1" / "Day 3" (M2-44, M2 hi-fi mock). */
    id: 'curing-day-labels',
    afterDatasetsDraw: (chart) => {
      const xAxis = chart.scales['x'];
      if (!xAxis) return;

      const { ctx, chartArea } = chart;

      ctx.save();
      const style = this.document.defaultView?.getComputedStyle(this.document.documentElement);
      ctx.fillStyle = style?.getPropertyValue('--p-surface-950').trim() || '#171717';
      ctx.font = `${Math.round((this.fontSize() * 11) / 12)}px ${this.colors().font}`;
      ctx.textAlign = 'left';

      for (const day of [1, 3]) {
        const x = xAxis.getPixelForValue(day);
        ctx.fillText(`Day ${day}`, x + 8, chartArea.top + this.fontSize());
      }

      ctx.restore();
    },
  };

  /** Hovered day plugin (M2-44, M2 hi-fi mock). */
  private readonly hoverPlugin = chartHoverPlugin(
    () => this.hoveredDay(),
    (day) => this.hoveredDayChange.emit(day),
  );

  private readonly redrawOnHover = effect(() => {
    if (this.printMode()) return;
    this.hoveredDay();
    this.chartWidget()?.chart?.draw();
  });
}
