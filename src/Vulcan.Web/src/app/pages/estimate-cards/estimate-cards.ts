import { DatePipe, DecimalPipe, formatNumber } from '@angular/common';
import { Component, computed, input } from '@angular/core';

import { AdjustmentStage, BatchDetail, TestAge } from '../../core/api/models';
import { StrengthLoad } from '../strength-chart/strength-chart';

const AGES: { horizon: 3 | 7 | 28; testAge: TestAge }[] = [
  { horizon: 3, testAge: 'Day3' },
  { horizon: 7, testAge: 'Day7' },
  { horizon: 28, testAge: 'Day28' },
];

/** How each estimate was adjusted (ADR-005 section 4.4), as the card says it. */
const ADJUSTMENTS: Record<AdjustmentStage, string> = {
  None: 'Before any breaks',
  ThreeDay: 'Adjusted using the 3-day breaks',
  ThreeAndSevenDay: 'Adjusted using the 3- and 7-day breaks',
};

const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * The 3-, 7- and 28-day estimate cards under the chart (SCRUM-175, FR-14, FR-15): each estimate,
 * how it was made and its calibration version, next to the actual break once there is one.
 * The 28-day card turns amber when the batch is at risk (client-decisions section 13).
 */
@Component({
  selector: 'app-estimate-cards',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './estimate-cards.html',
  styleUrl: './estimate-cards.scss',
})
export class EstimateCards {
  readonly state = input.required<StrengthLoad>();
  readonly batch = input.required<BatchDetail>();

  protected readonly cards = computed(() => {
    const state = this.state();
    const strength = state.status === 'loaded' ? state.strength : null;
    const batch = this.batch();
    const atRisk = strength?.atRisk ?? false;

    return AGES.map(({ horizon, testAge }) => {
      const estimate = strength?.estimates.find((e) => e.horizonDays === horizon) ?? null;
      const breaks = batch.specimens
        .filter((s) => s.testAgeDays === testAge && s.breakResult)
        .map((s) => s.breakResult!.strengthPsi);
      const actual =
        breaks.length > 0 ? breaks.reduce((sum, psi) => sum + psi, 0) / breaks.length : null;
      const estimatedPsi = estimate?.estimatedPsi ?? null;
      return {
        horizon,
        estimate,
        estimatedPsi,
        adjustment: estimate ? ADJUSTMENTS[estimate.adjustmentStage] : '',
        adjusted: estimate !== null && estimate.adjustmentStage !== 'None',
        actual: actual === null ? null : Math.round(actual),
        breakCount: breaks.length,
        difference:
          actual === null || estimatedPsi === null ? null : difference(estimatedPsi, actual),
        dueAt: new Date(Date.parse(batch.castAt) + horizon * DAY_MS),
        atRisk: horizon === 28 && atRisk,
      };
    });
  });

  /** "28% below target", or the minimum-strength warning when that's the only one. */
  protected readonly riskLabel = computed(() => {
    const state = this.state();
    if (state.status !== 'loaded' || !state.strength.atRisk) return '';
    const { earlyWarnings, shortfallPct } = state.strength;
    if (earlyWarnings.some((w) => w.limitKey === 'Prediction.ShortfallPct') && shortfallPct) {
      return `${Math.round(shortfallPct)}% below target`;
    }
    const minimum = earlyWarnings.find((w) => w.limitKey === 'Prediction.MinimumPsi');
    return minimum ? `Below the ${minimum.limit.toLocaleString('en-US')} PSI minimum` : '';
  });
}

/** ADR-005 Equation 7, signed: "+1.2%" when the estimate is above the actual break, "−0.3%" below. */
function difference(estimatedPsi: number, actualPsi: number): string {
  const pct = Math.round(((estimatedPsi - actualPsi) / actualPsi) * 1000) / 10;
  if (pct === 0) return '0.0%';
  return `${pct < 0 ? '−' : '+'}${formatNumber(Math.abs(pct), 'en-US', '1.1-1')}%`;
}
