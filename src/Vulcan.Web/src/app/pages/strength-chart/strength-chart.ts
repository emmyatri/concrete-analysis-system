import { DOCUMENT } from '@angular/common';
import { Component, computed, effect, inject, input, output, viewChild } from '@angular/core';
import { ChartModule, UIChart } from 'primeng/chart';
import type { Plugin } from 'chart.js';
import { BatchStrength } from '../../core/api/models';
import { StrengthChartColors, endLabelsPlugin, strengthChartConfig } from './strength-chart-config';
import { chartHoverPlugin } from '../../shared/chart-hover/chart-hover-plugin';

/** The strength request, as Batch Detail tracks it. */
export type StrengthLoad =
  | { status: 'loading' }
  | { status: 'loaded'; strength: BatchStrength }
  | { status: 'failed'; message: string };

/**
 * Strength (PSI) over days since cast for one batch (SCRUM-174, FR-11, mockups M2-01 and M2-02).
 * Its own component so chart.js and its styles stay out of Batch Detail.
 */
@Component({
  selector: 'app-strength-chart',
  imports: [ChartModule],
  templateUrl: './strength-chart.html',
  styleUrl: './strength-chart.scss',
})
export class StrengthChart {
  private readonly document = inject(DOCUMENT);

  readonly state = input.required<StrengthLoad>();
  /** The batch's cast time (ISO, UTC), for "Latest reading: 18 h". */
  readonly castAt = input.required<string>();
  /** The minimum strength line, from the configured Warn limit. Null hides it until the limits load. */
  readonly minimumPsi = input.required<number | null>();
  /** Cylinders whose break was saved with a warning, drawn in orange. */
  readonly flaggedSpecimenIds = input<ReadonlySet<number>>(new Set());

  /** Chart height; the expanded view on Batch Detail makes it taller. */
  readonly height = input('320px');
  /** Text size for axes, legend and labels; the expanded view makes it larger. */
  readonly fontSize = input(12);

  /** Day hovered over by either strength or curing charts. (M2-44) */
  readonly hoveredDay = input<number | null>(null);
  readonly hoveredDayChange = output<number | null>();
  private readonly chartWidget = viewChild(UIChart);

  /** Render the chart without interactive behavior for reports. */
  readonly printMode = input(false);

  protected readonly plugins = computed<Plugin<'line'>[]>(() => {
    if (this.printMode()) {
      return [endLabelsPlugin];
    }

    return [
      endLabelsPlugin,
      chartHoverPlugin(
        () => this.hoveredDay(),
        (day) => this.hoveredDayChange.emit(day),
      ),
    ];
  });

  private readonly redrawOnHover = effect(() => {
    if (this.printMode()) return;

    this.hoveredDay();
    this.chartWidget()?.chart?.draw();
  });

  protected readonly strength = computed(() => {
    const state = this.state();
    return state.status === 'loaded' ? state.strength : null;
  });

  protected readonly failure = computed(() => {
    const state = this.state();
    return state.status === 'failed' ? state.message : null;
  });

  protected readonly config = computed(() => {
    const strength = this.strength();
    if (!strength) return null;

    const config = strengthChartConfig(
      strength,
      this.minimumPsi(),
      this.flaggedSpecimenIds(),
      this.colors(),
      this.fontSize(),
    );

    if (this.printMode()) {
      config.options.events = [];

      if (config.options.plugins?.tooltip) {
        config.options.plugins.tooltip.enabled = false;
      }

      // Keep the report legend compact without changing Batch Detail.
      const legend = config.options.plugins?.legend;

      if (legend && legend.labels) {
        legend.labels.font = {
          size: 10,
          family: 'Inter, sans-serif',
        };

        legend.labels.padding = 8;
        legend.labels.boxWidth = 10;
        legend.labels.boxHeight = 5;
      }
    }

    return config;
  });

  /** Read out by screen readers in place of the chart. */
  protected readonly summary = computed(() => {
    const strength = this.strength();
    if (!strength) return '';
    const expected = `Expected final ${strength.expectedFinalBreakPsi.toLocaleString('en-US')} PSI`;
    const breaks = `${strength.measuredBreaks.length} breaks recorded`;
    return strength.projected28DayPsi === null
      ? `${strength.batchNumber} strength over time. Not enough curing data to estimate yet. ${expected}. ${breaks}.`
      : `${strength.batchNumber} strength over time. Predicted 28-day strength ${strength.projected28DayPsi.toLocaleString('en-US')} PSI. ${expected}. ${breaks}.`;
  });

  /** Hours from cast to the newest curing reading, or null when there are none. */
  protected readonly latestReadingHours = computed(() => {
    const times = (this.strength()?.curingPoints ?? []).map((p) => Date.parse(p.time));
    if (times.length === 0) return null;
    return Math.floor((Math.max(...times) - Date.parse(this.castAt())) / 3_600_000);
  });

  /** The theme's colors (vulcan-preset.ts), so the chart follows the theme like the rest of the app. */
  private colors(): StrengthChartColors {
    const style = this.document.defaultView?.getComputedStyle(this.document.documentElement);
    const token = (name: string, fallback: string) =>
      style?.getPropertyValue(name).trim() || fallback;
    return {
      predicted: token('--p-blue-500', '#3b82f6'),
      initialEstimate: token('--p-blue-300', '#93c5fd'),
      breaks: token('--p-primary-500', '#263a78'),
      flagged: token('--p-orange-500', '#f97316'),
      target: token('--p-surface-900', '#21242b'),
      minimum: token('--p-orange-600', '#ea580c'),
      text: token('--p-text-muted-color', '#696e79'),
      grid: token('--p-surface-200', '#dddfe4'),
      surface: token('--p-surface-0', '#ffffff'),
      font: style?.getPropertyValue('font-family').trim() || 'Inter, sans-serif',
    };
  }
}
