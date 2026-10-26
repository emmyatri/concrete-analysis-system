import { ChartModule } from 'primeng/chart';
import { SelectModule } from 'primeng/select';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { Component, computed, inject, input, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { CuringPoint, CuringReading } from '../../core/api/models';
import { type CuringMeasurement, type CuringChartColors, curingChartConfig, cylinderAppearance, measurementLabels } from './curing-chart-config';
import { SavedWarning} from '../../core/api/models';

/**
 * Curing reading over days since cast for one batch (SCRUM-176, FR-12, mockups M2-01 and M2-02).
 * Its own component so chart.js and its styles stay out of Batch Detail.
 */
@Component({
  selector: 'app-curing-chart',
  imports: [ChartModule, SelectModule, FormsModule, ButtonModule],
  templateUrl: './curing-chart.html',
  styleUrl: './curing-chart.scss'
})
export class CuringChart {
  private readonly document = inject(DOCUMENT);

  /** Curing readings for the batch with warning details. */
  readonly readings = input.required<{label: string, reading: CuringReading, ambientWarning: SavedWarning | undefined}[]>();

  /** Curing measurements for the batch, or an empty array until strength data loads. */
  readonly curingPoints = input.required<CuringPoint[]>();

  /** The batch's cast time (ISO, UTC), for "Latest reading: 18 h". */
  readonly castAt = input.required<string>();

  /** The user's selected measurement for filtering the view of curing readings;
   * default = internal temp (M2-44).
   * */
  readonly selectedMeasurement = signal<CuringMeasurement>('internalTempF');

  readonly measurementOptions = Object.entries(measurementLabels).map(
    ([value, label]) => ({ value, label }),
  );

  readonly cylinders = computed(() => {
    const unique = new Map<number, string>();

    for (const point of this.curingPoints()) {
      unique.set(point.specimenId, point.specimenLabel);
    }
    return [...unique.entries()]
      .sort(([a], [b]) => a - b)
      .map(([id, label], index) => ({
        id,
        label,
        appearance: cylinderAppearance(index, this.colors().cylinders),
      }));
  });

  readonly hiddenCylinderIds = signal<ReadonlySet<number>>(new Set<number>());

  /** All cylinders displayed by default, clicking one hides/displays the cylinder.*/
  toggleCylinder(id: number): void {
    this.hiddenCylinderIds.update(current => {
      const next = new Set(current);

      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }

      return next;
    });
  }
  showAllCylinders(): void {
    this.hiddenCylinderIds.set(new Set<number>());
  }
  hideAllCylinders(): void {
    this.hiddenCylinderIds.set(
      new Set(this.cylinders().map(cylinder => cylinder.id))
    );
  }

  /** Maps the ambient warnings to its respective curing point for chart rendering.*/
  protected readonly flaggedReadings = computed(
    () =>
      new Set((this.readings()).filter((s) => s.ambientWarning)
          .map((s) => `${s.label} | ${Date.parse(s.reading.takenAt)}`)));

  /** Identifies the configuration pieces for the curing chart.*/
  protected readonly chart = computed(() =>
    curingChartConfig(
      this.curingPoints(),
      this.castAt(),
      this.colors(),
      this.selectedMeasurement(),
      this.hiddenCylinderIds(),
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
      cylinders: [
        token('--p-cyan-800', '#155E75'),
        token('--p-emerald-600', '#059669'),
        token('--p-primary-500', '#263A78'),
        token('--p-pink-900', '#831843'),
      ],
      flagged: token('--p-orange-500', '#F97316'),
    };
  }
}
