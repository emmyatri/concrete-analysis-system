import { calculateComposition } from './composition-calc';

// Same values as the SCRUM-96 seed (specific-gravity.json).
const gravities = new Map<string, number>([
  ['Cement', 3.15],
  ['Water', 1.0],
  ['FlyAsh', 2.35],
  ['Slag', 2.9],
  ['SilicaFume', 2.35],
]);

describe('calculateComposition', () => {
  it('matches the hi-fi mockup: 16/17/40/25 with 2% fly ash → total 100, air 0, w/c 0.31', () => {
    const totals = calculateComposition(
      { cementPct: 16, waterPct: 17, coarseAggregatePct: 40, fineAggregatePct: 25, scmPct: 2, scmType: 'FlyAsh' },
      gravities,
    );

    expect(totals).toEqual({ totalPct: 100, airOtherPct: 0, waterCementRatio: 0.31 });
  });

  it('leaves SCM out of the ratio when the SCM type is None', () => {
    const totals = calculateComposition(
      { cementPct: 12, waterPct: 18, coarseAggregatePct: 45, fineAggregatePct: 25, scmPct: 0, scmType: 'None' },
      gravities,
    );

    expect(totals.waterCementRatio).toBe(0.48); // 18 ÷ (12 × 3.15)
    expect(totals.airOtherPct).toBe(0);
  });

  it('has no ratio until cement or SCM is entered', () => {
    const totals = calculateComposition(
      { cementPct: null, waterPct: 17, coarseAggregatePct: null, fineAggregatePct: null, scmPct: null, scmType: 'None' },
      gravities,
    );

    expect(totals.waterCementRatio).toBeNull();
  });
});
