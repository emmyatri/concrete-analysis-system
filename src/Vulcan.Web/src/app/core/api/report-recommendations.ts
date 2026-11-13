import { BatchStrength, SavedWarning } from './models';

export interface ReportRecommendation {
  code: string;
  title: string;
  description: string;
}

/**
 * Generates engineering review guidance from existing
 * strength assessments and recorded validation warnings.
 *
 * Recommendations are advisory only and do not
 * determine whether a concrete batch is acceptable.
 */
export function buildReportRecommendations(
  strength: BatchStrength,
  warnings: readonly SavedWarning[],
): ReportRecommendation[] {
  const recommendations: ReportRecommendation[] = [];
  const added = new Set<string>();

  function add(code: string, title: string, description: string): void {
    if (added.has(code)) return;

    added.add(code);
    recommendations.push({ code, title, description });
  }

  // Strength assessment
  if (strength.atRisk) {
    add(
      'strength-risk',
      'Review projected compressive strength',
      'Compare the projected 28-day strength with the specified target. ' +
        'Review available break results and investigate contributing ' +
        'mix and curing conditions before making an acceptance determination.',
    );
  }

  if (strength.status === 'InsufficientCuringData') {
    add(
      'curing-data',
      'Verify curing data availability',
      'Confirm that sufficient curing temperature readings have been ' +
        'recorded to support maturity calculations and strength estimates.',
    );
  }

  // Combine recorded validation warnings and early-strength warnings.
  const warningCodes = new Set<string>([
    ...warnings.map((warning) => warning.limitKey),
    ...strength.earlyWarnings.map((warning) => warning.limitKey),
  ]);

  if (warningCodes.has('Composition.WaterCementRatio')) {
    add(
      'water-cement-ratio',
      'Review water-to-cementitious ratio',
      'Verify the recorded mix proportions and calculated ' +
        'water-to-cementitious ratio. Evaluate whether the mix ' +
        'meets the applicable design requirements.',
    );
  }

  if (warningCodes.has('Curing.AmbientTempF')) {
    add(
      'curing-temperature',
      'Review curing temperature conditions',
      'Examine recorded curing temperatures and investigate ' +
        'conditions outside the configured normal range.',
    );
  }

  if (warningCodes.has('Curing.MoisturePct')) {
    add(
      'curing-moisture',
      'Review specimen moisture conditions',
      'Verify moisture readings expressed as a percentage of ' +
        'sample weight and investigate values outside the ' +
        'configured normal range.',
    );
  }

  if (warningCodes.has('Break.StrengthPsi')) {
    add(
      'break-strength',
      'Review recorded compressive-strength tests',
      'Verify the flagged break results, specimen records, ' +
        'and testing conditions against the applicable requirements.',
    );
  }

  if (warningCodes.has('Batch.MixWaterPh') || warningCodes.has('Batch.CementSurfacePh')) {
    add(
      'ph-review',
      'Review recorded pH measurements',
      'Verify the flagged pH measurements and investigate ' +
        'potential inconsistencies with the configured limits.',
    );
  }

  const oxideCodes = [
    'Oxide.AluminaPct',
    'Oxide.IronOxidePct',
    'Oxide.MagnesiumOxidePct',
    'Oxide.SulfurTrioxidePct',
  ];

  if (oxideCodes.some((code) => warningCodes.has(code))) {
    add(
      'cement-chemistry',
      'Review cement chemistry',
      'Verify the flagged cement oxide concentrations against ' +
        'the recorded cement type and applicable material specifications.',
    );
  }

  // General follow-up for warning types without a dedicated rule.
  if (warningCodes.size > 0 && recommendations.length === 0) {
    add(
      'validation-review',
      'Review reported warning conditions',
      'Investigate the reported warning conditions and verify ' +
        'the relevant batch, curing, and testing records against ' +
        'applicable requirements.',
    );
  }

  return recommendations;
}
