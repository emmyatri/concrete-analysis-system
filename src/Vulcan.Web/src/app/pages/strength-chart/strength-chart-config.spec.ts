import { MOCK_BATCH_DETAILS } from '../../core/api/mock/mock-batch-details';
import { mockStrength } from '../../core/api/mock/mock-strength';
import { StrengthChartColors, strengthChartConfig } from './strength-chart-config';

const COLORS: StrengthChartColors = {
  predicted: 'blue',
  initialEstimate: 'lightblue',
  breaks: 'navy',
  flagged: 'orange',
  target: 'black',
  minimum: 'darkorange',
  text: 'gray',
  grid: 'lightgray',
  font: 'Inter',
};

const strengthOf = (batchNumber: string) =>
  mockStrength(MOCK_BATCH_DETAILS.find((b) => b.batchNumber === batchNumber)!);

describe('strengthChartConfig', () => {
  it('labels every batch line by batch number, flagged breaks in their own set', () => {
    const strength = strengthOf('VM-SYN-006');
    const d31 = strength.measuredBreaks.find((b) => b.specimenLabel === 'D3-1')!;

    const { data } = strengthChartConfig(strength, 3000, new Set([d31.specimenId]), COLORS);

    expect(data.datasets.map((d) => d.label)).toEqual([
      'VM-SYN-006 predicted',
      'VM-SYN-006 before breaks',
      'VM-SYN-006 breaks',
      'VM-SYN-006 flagged breaks',
      'VM-SYN-006 mean by age',
      'Expected final',
      'Minimum',
    ]);
    expect(data.datasets[3].data).toEqual([{ x: 3, y: 2950, specimen: 'D3-1' }]);
    expect(data.datasets[3].borderColor).toBe('orange');
  });

  it('starts the curve at zero on the day of cast and runs to day 28', () => {
    const { data } = strengthChartConfig(strengthOf('VM-SYN-006'), 3000, new Set(), COLORS);

    const predicted = data.datasets[0].data;
    expect(predicted[0]).toEqual({ x: 0, y: 0 });
    expect(predicted.at(-1)).toEqual({ x: 28, y: 4449 });
  });

  it('draws the mean break at each age', () => {
    const { data } = strengthChartConfig(strengthOf('VM-SYN-006'), 3000, new Set(), COLORS);

    const mean = data.datasets.find((d) => d.label === 'VM-SYN-006 mean by age')!;
    expect(mean.data.map((p) => p.y)).toEqual([3208, 3903, 4461]);
  });

  it('fits the y-axis to the highest of the curves, breaks and Expected final', () => {
    // VM-SYN-001's highest value is a 28-day break of 6,740, above its 6,200 Expected final.
    const { options } = strengthChartConfig(strengthOf('VM-SYN-001'), 3000, new Set(), COLORS);

    expect(options.scales?.['y']?.max).toBe(8000);
  });

  it('uses 1,000 PSI gridlines for a low-strength batch', () => {
    const strength = { ...strengthOf('VM-2026-0142'), expectedFinalBreakPsi: 4000 };

    const { options } = strengthChartConfig(strength, 3000, new Set(), COLORS);

    expect(options.scales?.['y']?.max).toBe(5000);
  });

  it('shows only Expected final and the minimum without curing data or breaks', () => {
    const { data } = strengthChartConfig(strengthOf('VM-2026-0142'), 3000, new Set(), COLORS);

    expect(data.datasets.map((d) => d.label)).toEqual(['Expected final', 'Minimum']);
  });

  it('leaves out the minimum line until the limits have loaded', () => {
    const { data } = strengthChartConfig(strengthOf('VM-2026-0142'), null, new Set(), COLORS);

    expect(data.datasets.map((d) => d.label)).toEqual(['Expected final']);
  });
});
