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
  surface: 'white',
  font: 'Inter',
};

const strengthOf = (batchNumber: string) =>
  mockStrength(MOCK_BATCH_DETAILS.find((b) => b.batchNumber === batchNumber)!);

describe('strengthChartConfig', () => {
  it('labels every line without repeating the batch number, flagged breaks in their own set', () => {
    const strength = strengthOf('VM-SYN-006');
    const d31 = strength.measuredBreaks.find((b) => b.specimenLabel === 'D3-1')!;

    const { data } = strengthChartConfig(strength, 3000, new Set([d31.specimenId]), COLORS);

    expect(data.datasets.map((d) => d.label)).toEqual([
      'Predicted',
      'Before breaks',
      'Estimate',
      'Breaks',
      'Flagged breaks',
      'Mean by age',
      'Expected final',
      'Minimum',
    ]);
    expect(data.datasets[4].data).toEqual([{ x: 3, y: 3148, specimen: 'D3-1' }]);
    expect(data.datasets[4].borderColor).toBe('orange');
  });

  it('marks the 3-, 7- and 28-day estimates with hollow markers', () => {
    const { data } = strengthChartConfig(strengthOf('VM-SYN-006'), 3000, new Set(), COLORS);

    const estimate = data.datasets.find((d) => d.label === 'Estimate')!;
    expect(estimate.data).toEqual([
      { x: 3, y: 3129 },
      { x: 7, y: 3969 },
      { x: 28, y: 4733 },
    ]);
    expect([estimate.borderColor, estimate.backgroundColor]).toEqual(['blue', 'white']);
  });

  it('starts the curve at zero on the day of cast and runs to day 28', () => {
    const { data } = strengthChartConfig(strengthOf('VM-SYN-006'), 3000, new Set(), COLORS);

    const predicted = data.datasets[0].data;
    expect(predicted[0]).toEqual({ x: 0, y: 0 });
    expect(predicted.at(-1)).toEqual({ x: 28, y: 4733 });
  });

  it('draws the mean break at each age as a line', () => {
    const { data } = strengthChartConfig(strengthOf('VM-SYN-006'), 3000, new Set(), COLORS);

    const mean = data.datasets.find((d) => d.label === 'Mean by age')!;
    expect(mean.data.map((p) => p.y)).toEqual([3129, 3969, 4994]);
    expect(mean.showLine).not.toBe(false);
  });

  it('labels the 28-day estimate with its gap to target, and the 28-day actual', () => {
    const { data } = strengthChartConfig(strengthOf('VM-SYN-006'), 3000, new Set(), COLORS);
    const endLabels = data.datasets
      .map((d) => (d as { endLabel?: string }).endLabel)
      .filter(Boolean);

    expect(endLabels).toEqual([
      '28-day est. 4,733 (−24%)',
      '28-day actual 4,994',
      'Expected final 6,200',
      '3,000 min',
    ]);
  });

  it('shows a projection above target as a plus', () => {
    const { data } = strengthChartConfig(strengthOf('VM-SYN-005'), 3000, new Set(), COLORS);

    expect((data.datasets[0] as { endLabel?: string }).endLabel).toBe('28-day est. 6,106 (+7%)');
  });

  it('puts the end labels in a right-hand gutter that grows with the text', () => {
    const small = strengthChartConfig(strengthOf('VM-SYN-006'), 3000, new Set(), COLORS);
    const large = strengthChartConfig(strengthOf('VM-SYN-006'), 3000, new Set(), COLORS, 16);

    expect(small.options.layout?.padding).toMatchObject({ right: 180 });
    expect(large.options.layout?.padding).toMatchObject({ right: 240 });
  });

  it('fits the y-axis to the highest of the curves, breaks and Expected final', () => {
    // VM-SYN-001's highest value is a 28-day break of 6,445, above its 6,200 Expected final.
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
