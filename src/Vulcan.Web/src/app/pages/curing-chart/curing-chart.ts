import { ChartModule } from 'primeng/chart';
import {Component, computed, inject, input, signal} from '@angular/core';
import { CuringPoint } from '../../core/api/models';
import {type CuringMeasurement, type CuringChartColors, curingChartConfig} from './curing-chart-config';
import {DOCUMENT} from '@angular/common';

/**
 * Curing reading over days since cast for one batch (SCRUM-176, FR-12, mockups M2-01 and M2-02).
 * Its own component so chart.js and its styles stay out of Batch Detail.
 */
@Component({
  selector: 'app-curing-chart',
  imports: [ChartModule],
  templateUrl: './curing-chart.html',
  styleUrl: './curing-chart.scss'
})
export class CuringChart {
  private readonly document = inject(DOCUMENT);

  /** Curing measurements for the batch, or an empty array until strength data loads. */
  readonly curingPoints = input.required<CuringPoint[]>();

  /** The batch's cast time (ISO, UTC), for "Latest reading: 18 h". */
  readonly castAt = input.required<string>();

  /** The user's selected measurement for filtering the view of curing readings.
   * default value = internal temp.
   */
  readonly selectedMeasurement = signal<CuringMeasurement>('internalTempF');

  /** The theme's colors (vulcan-preset.ts), so the chart follows the theme like the rest of the app. */
  private colors(): CuringChartColors {
    const style = this.document.defaultView?.getComputedStyle(this.document.documentElement);
    const token = (name: string, fallback: string) =>
      style?.getPropertyValue(name).trim() || fallback;
    return {
      text: token('--p-text-muted-color', '#696e79'),
      grid: token('--p-surface-200', '#dddfe4'),
      font: style?.getPropertyValue('font-family').trim() || 'Inter, sans-serif',
    };
  }

  /** Identifies the configuration pieces for the curing chart.*/
  protected readonly chart = computed(() =>
    curingChartConfig(
      this.curingPoints(),
      this.castAt(),
      this.colors(),
      this.selectedMeasurement(),
    ),
  );
}
