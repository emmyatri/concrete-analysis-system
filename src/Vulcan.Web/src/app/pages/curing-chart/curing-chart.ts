import { ChartModule } from 'primeng/chart';
import { Component, computed, inject, input, signal } from '@angular/core';
import { CuringPoint } from '../../core/api/models';
import type { CuringMeasurement } from './curing-chart-config';

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

  /** Curing measurements for the batch, or an empty array until strength data loads. */
  readonly curingPoints = input.required<CuringPoint[]>();

  /** The batch's cast time (ISO, UTC), for "Latest reading: 18 h". */
  readonly castAt = input.required<string>();

  /** The user's selected measurement for filtering the view of curing readings.
   * default value = internal temp.
   */
  readonly selectedMeasurement = signal<CuringMeasurement>('internalTempF');

}
