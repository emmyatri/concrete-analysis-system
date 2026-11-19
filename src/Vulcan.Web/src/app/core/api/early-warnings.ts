import { EarlyWarning } from './models';

/**
 * Strength shortfalls first, then low early breaks (client-decisions section 14.1), then mix and
 * curing reasons, as on the M2-45 banner.
 */
export function earlyWarningsShortfallFirst(warnings: readonly EarlyWarning[]): EarlyWarning[] {
  return [...warnings].sort((a, b) => rank(a) - rank(b));
}

function rank(warning: EarlyWarning): number {
  if (warning.limitKey === 'Prediction.ShortfallPct') return 0;
  if (warning.limitKey === 'Prediction.MinimumPsi') return 1;
  if (warning.limitKey === 'Break.Day7FailPct') return 2;
  if (warning.limitKey === 'Break.Day3WarnPct') return 3;
  return 4;
}
