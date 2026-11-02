import { EarlyWarning } from './models';

/** Strength shortfalls first, then mix and curing reasons, as on the M2-45 banner. */
export function earlyWarningsShortfallFirst(warnings: readonly EarlyWarning[]): EarlyWarning[] {
  return [...warnings].sort((a, b) => rank(a) - rank(b));
}

function rank(warning: EarlyWarning): number {
  if (warning.limitKey === 'Prediction.ShortfallPct') return 0;
  if (warning.limitKey === 'Prediction.MinimumPsi') return 1;
  return 2;
}
