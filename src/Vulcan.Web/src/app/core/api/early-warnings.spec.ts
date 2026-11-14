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

  it('puts low early breaks after the shortfall, the 7-day one first', () => {
    const ordered = earlyWarningsShortfallFirst([
      warning('Batch.MixWaterPh', 'pH'),
      warning('Break.Day3WarnPct', '3-day'),
      warning('Break.Day7FailPct', '7-day'),
      warning('Prediction.ShortfallPct', 'shortfall'),
    ]);

    expect(ordered.map((w) => w.limitKey)).toEqual([
      'Prediction.ShortfallPct',
      'Break.Day7FailPct',
      'Break.Day3WarnPct',
      'Batch.MixWaterPh',
    ]);
  });
});
