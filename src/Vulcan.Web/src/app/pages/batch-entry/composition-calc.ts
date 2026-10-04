import { ScmType } from '../../core/api/batches';

export interface CompositionInput {
  cementPct: number | null;
  waterPct: number | null;
  coarseAggregatePct: number | null;
  fineAggregatePct: number | null;
  scmPct: number | null;
  scmType: ScmType;
}

export interface CompositionTotals {
  totalPct: number;
  airOtherPct: number;
  /** Water ÷ cementitious, by weight. Null until it can be calculated. */
  waterCementRatio: number | null;
}

/**
 * The "Calculated · not stored" values (schema.md, Decision 1a).
 * Mix percentages are by absolute volume; w/c converts them to weight with specific gravities:
 * (Water × SG_water) ÷ (Cement × SG_cement + SCM × SG_scmType). Null if the denominator is 0.
 */
export function calculateComposition(
  input: CompositionInput,
  gravities: ReadonlyMap<string, number>,
): CompositionTotals {
  const n = (value: number | null) => value ?? 0;
  const totalPct =
    n(input.cementPct) + n(input.waterPct) + n(input.coarseAggregatePct) + n(input.fineAggregatePct) + n(input.scmPct);

  const sgWater = gravities.get('Water');
  const sgCement = gravities.get('Cement');
  const sgScm = input.scmType === 'None' ? 0 : gravities.get(input.scmType);

  let waterCementRatio: number | null = null;
  if (sgWater !== undefined && sgCement !== undefined && sgScm !== undefined && input.waterPct !== null) {
    const cementitious = n(input.cementPct) * sgCement + n(input.scmPct) * sgScm;
    waterCementRatio = cementitious > 0 ? (input.waterPct * sgWater) / cementitious : null;
  }

  return {
    totalPct: round2(totalPct),
    airOtherPct: round2(100 - totalPct),
    waterCementRatio: waterCementRatio === null ? null : round2(waterCementRatio),
  };
}

function round2(value: number): number {
  return Math.round(value * 100) / 100;
}
