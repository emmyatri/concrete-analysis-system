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
  output,
  signal,
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

  /** Curing readings for the batch with warning details. */
  readonly readings = input.required<
    {
      label: string;
      reading: CuringReading;
      ambientWarning: SavedWarning | undefined;
    }[]
  >();

  /** Curing measurements for the batch, or an empty array until strength data loads. */
  readonly curingPoints = input.required<CuringPoint[]>();
  /** The batch's cast time (ISO, UTC), for "Latest reading: 18 h". */
  readonly castAt = input.required<string>();

  /** Day hovered over by either strength or curing charts. (M2-42) */
  readonly hoveredDay = input<number | null>(null);
  readonly hoveredDayChange = output<number | null>();
  private readonly chartWidget = viewChild(UIChart);

  /** The user's selected measurement for filtering the view of curing readings;
   * default = internal temp (M2-44).
   * */
  readonly selectedMeasurement = signal<CuringMeasurement>('internalTempF');
  readonly measurementOptions = Object.entries(measurementLabels).map(([value, label]) => ({
    value,
    label,
  }));

  /** Maps the ambient warnings to its respective curing point for chart rendering.*/
  protected readonly flaggedReadings = computed(
    () =>
      new Map(
        this.readings()
          .filter((s) => s.ambientWarning)
          .map(
            (s) =>
              [`${s.label} | ${Date.parse(s.reading.takenAt)}`, s.ambientWarning!.message] as const,
          ),
      ),
  );

  /** Caption inside the strip: "Line = average of 9 cylinders · band = lowest to highest ·
   * readings so far: 0–72 h" (use the real cylinder count and hours). (M2-42)
   * */
  protected readonly cylinderCount = computed(
    () => new Set(this.readings().map((s) => s.label)).size,
  );

  readonly readingCaption = computed(() => {
    const hours = this.chart().readingHours;
    const cylinders = this.cylinderCount();
    const firstHour = Math.round(hours[0]);
    const lastHour = Math.round(hours[hours.length - 1]);

    if (hours.length === 0) {
      return 'No curing readings yet';
    }
    /** Could have multiple readings across cylinders (9 cylinders x 2 readings).
     * Count based on hours in array not individual cylinders' readings.
     */
    if (hours.length === 1) {
      return `${hours.length} reading so far (${firstHour} h)`;
    }
    if (hours.length === 2) {
      return `${hours.length} readings so far (${firstHour} h and ${lastHour} h)`;
    }
    return `Line = average of ${cylinders} cylinders · band = lowest to highest · readings so far: ${firstHour}–${lastHour}h`;
  });

  /** Identifies the configuration pieces for the curing chart.*/
  protected readonly chart = computed(() =>
    curingChartConfig(
      this.curingPoints(),
      this.castAt(),
      this.colors(),
      this.selectedMeasurement(),
      this.flaggedReadings(),
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

  protected readonly guidePlugins: Plugin<'line'>[] = [
    {
      /** Faint vertical lines at Day 1 and Day 3, labeled "Day 1" / "Day 3" (M2-42, M2 hi-fi mock). */
      id: 'curing-day-labels',
      afterDatasetsDraw: (chart) => {
        const xAxis = chart.scales['x'];
        if (!xAxis) return;

        const { ctx, chartArea } = chart;

        ctx.save();
        const style = this.document.defaultView?.getComputedStyle(this.document.documentElement);
        ctx.fillStyle = style?.getPropertyValue('--p-surface-950').trim() || '#171717';
        ctx.font = `11px ${this.colors().font}`;
        ctx.textAlign = 'left';

        for (const day of [1, 3]) {
          const x = xAxis.getPixelForValue(day);
          ctx.fillText(`Day ${day}`, x + 8, chartArea.top + 12);
        }

        ctx.restore();
      },
    },

    /** Hovered day plugin (M2-42, M2 hi-fi mock). */
    chartHoverPlugin(
      () => this.hoveredDay(),
      (day) => this.hoveredDayChange.emit(day),
    ),
  ];

  private readonly redrawOnHover = effect(() => {
    this.hoveredDay();
    this.chartWidget()?.chart?.draw();
  });
}
