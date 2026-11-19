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
    const targetPct = strength?.accuracyTargetPct ?? null;

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
          actual === null || estimatedPsi === null
            ? null
            : difference(estimatedPsi, actual, horizon === 28 ? targetPct : null),
        target:
          actual === null || estimatedPsi === null || horizon !== 28 || targetPct === null
            ? null
            : accuracyTarget(estimatedPsi, actual, targetPct),
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

/** Returns the raw, unrounded, percentage of the actual break. */
function percentOff(estimated: number, actual: number): number {
  return ((estimated - actual) / actual) * 100;
}

/** Miss % as display text. Adds decimals (up to 3) when one decimal would
 * make an over-target miss look within the target.
 */
function formatPct(rawPct: number, targetPct: number | null): string {
  const maxDecimals = 3;
  const miss = Math.abs(rawPct);

  if (targetPct === null || miss <= targetPct) {
    const rounded = Math.round(miss * 10) / 10;
    return formatNumber(rounded, 'en-US', '1.1-1');
  }

  for (let d = 1; d <= maxDecimals; d++) {
    const factor = 10 ** d;
    const rounded = Math.round(miss * factor) / factor;

    if (rounded > targetPct) {
      return formatNumber(rounded, 'en-US', `1.${d}-${d}`);
    }
  }

  return '>' + formatNumber(targetPct, 'en-US', `1.${maxDecimals}-${maxDecimals}`);
}

/**
 * ADR-005 Equation 7 in words: "Estimate was 91 PSI (1.9%) above the actual break", "… below …",
 * or "Estimate matched the actual break" when they agree to within 0.05%. Also adds
 * precision near the accuracy target (client-decisions section 14.2) so an over-target
 * miss never displays as within.
 */
function difference(estimatedPsi: number, actualPsi: number, targetPct: number | null): string {
  const raw = percentOff(estimatedPsi, actualPsi);
  const pct = Math.round(raw * 10) / 10;
  if (pct === 0) return 'Estimate matched the actual break';
  const psi = formatNumber(Math.abs(Math.round(estimatedPsi - actualPsi)), 'en-US', '1.0-0');
  const percent = formatPct(raw, targetPct);
  return `Estimate was ${psi} PSI (${percent}%) ${pct > 0 ? 'above' : 'below'} the actual break`;
}

/** Displays notification at the 28-day mark whether prediction variance is within/outside
 * the accuracy target (client-decisions section 14.2).
 */
function accuracyTarget(estimatedPsi: number, actualPsi: number, targetPct: number): string {
  const miss = Math.abs(percentOff(estimatedPsi, actualPsi));
  const within = miss <= targetPct;
  return within ? `Within the ${targetPct}% target` : `Outside the ${targetPct}% target`;
}
