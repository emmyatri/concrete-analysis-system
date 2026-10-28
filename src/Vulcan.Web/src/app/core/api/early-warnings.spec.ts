import { earlyWarningsShortfallFirst } from './early-warnings';
import { EarlyWarning } from './models';

const warning = (limitKey: string, text: string): EarlyWarning => ({
  warning: text,
  field: limitKey,
  value: 1,
  limitKey,
  limit: 1,
});

describe('earlyWarningsShortfallFirst', () => {
  it('puts the 28-day shortfall ahead of mix reasons', () => {
    const ordered = earlyWarningsShortfallFirst([
      warning('Composition.WaterCementRatio', 'w/cm'),
      warning('Prediction.ShortfallPct', 'shortfall'),
      warning('Batch.ExpectedFinalVsWcmPct', 'table'),
    ]);

    expect(ordered.map((w) => w.limitKey)).toEqual([
      'Prediction.ShortfallPct',
      'Composition.WaterCementRatio',
      'Batch.ExpectedFinalVsWcmPct',
    ]);
  });
});
