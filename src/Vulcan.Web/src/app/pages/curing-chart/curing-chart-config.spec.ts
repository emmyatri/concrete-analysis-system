import { type CuringChartColors, curingChartConfig, yAxisRange } from './curing-chart-config';
import { MOCK_BATCH_DETAILS } from '../../core/api/mock/mock-batch-details';
import { mockStrength } from '../../core/api/mock/mock-strength';
import type { Scale, ScriptableContext } from 'chart.js';

const COLORS: CuringChartColors = {
  text: 'gray',
  grid: 'lightgray',
  font: 'Inter',
  line: 'black',
  shade: 'lightblue',
  flagged: 'orange',
};

const batch = MOCK_BATCH_DETAILS.find((b) => b.batchNumber === 'VM-SYN-006')!;
const strength = mockStrength(batch);

/** Params needed to test curingChartConfig */
const points = strength.curingPoints;
const castAt = batch.castAt;
const colors = COLORS;
const flaggedReadings = new Map<string, string>();

describe('curingChartConfig', () => {
  it('calculates average internal temperature from synthetic curing readings', () => {
    const selectedMeasurement = 'internalTempF';
    const { data } = curingChartConfig(
      points,
      castAt,
      colors,
      selectedMeasurement,
      flaggedReadings,
    );

    /** Confirm average. */
    expect(data.datasets[0].data[0].y).toBeCloseTo(78.8889, 3);

    /** Confirm min of shaded band. */
    expect(data.datasets[2].data[0].y).toBe(78);

    /** Confirm max of shaded band. */
    expect(data.datasets[1].data[0].y).toBe(80);

    /** Confirm shading between mix/max region. */
    expect(data.datasets[1].fill).toBe('+1');

    expect(data.datasets[2].fill).toBe(false);

    /** Confirm day. */
    expect(data.datasets[0].data[0].x).toBe(0);

    /** Confirm dataset labels. */
    expect(data.datasets.map((d) => d.label)).toEqual(['Average', 'Maximum', 'Minimum']);

    /** Confirm the line and band END at the last reading (by count). */
    const distinctTimes = new Set(points.map((p) => p.time)).size;
    expect(data.datasets[0].data).toHaveLength(distinctTimes);

    /** Confirm the line and band END at the last reading (by last timestamp). */
    const lastTimestamp = Math.max(...points.map((p) => Date.parse(p.time)));

    const expectedLastDay = (lastTimestamp - Date.parse(castAt)) / 86_400_000;

    const averagePoints = data.datasets[0].data;

    expect(averagePoints[averagePoints.length - 1].x).toBeCloseTo(expectedLastDay, 5);
  });

  it('marks the average point when any cylinder has an ambient warning', () => {
    /** Arrange mock warning. */
    const flaggedPoint = points[0];
    const warningKey = `${flaggedPoint.specimenLabel} | ${Date.parse(flaggedPoint.time)}`;
    const warningMap = new Map<string, string>([[warningKey, 'Test ambient warning']]);
    const selectedMeasurement = 'ambientTempF';

    /** Test that markerColor is a function and returns the color change on ambient warning.*/
    const { data } = curingChartConfig(points, castAt, COLORS, selectedMeasurement, warningMap);

    const markerColor = data.datasets[0].pointBackgroundColor;

    if (typeof markerColor !== 'function') {
      throw new Error('Expected a point color callback');
    }

    expect(markerColor({ dataIndex: 0 } as ScriptableContext<'line'>, {})).toBe(COLORS.flagged);
  });

  it('leaves points unmarked when no reading at that time was flagged', () => {
    const { data } = curingChartConfig(points, castAt, COLORS, 'internalTempF', new Map());

    const markerColor = data.datasets[0].pointBackgroundColor;
    if (typeof markerColor !== 'function') {
      throw new Error('Expected a point color callback');
    }
    expect(markerColor({ dataIndex: 0 } as ScriptableContext<'line'>, {})).toBe(COLORS.line);
  });

  /** Parameterized test; tests the six curing measurements numeric outputs and respective unit. */
  it.each([
    ['internalTempF', 78.8889, '°F'],
    ['ambientTempF', 69.5, '°F'],
    ['moisturePct', 91.1222, '%'],
    ['relativeHumidityPct', 95.9444, '%'],
    ['resistivityOhmPerFt', 14.7222, 'Ω/ft'],
    ['co2Ppm', 420.9222, 'ppm'],
  ] as const)(
    'plots %s with the correct average and unit',
    (measurement, expectedAverage, expectedUnit) => {
      const { data, options } = curingChartConfig(
        points,
        castAt,
        COLORS,
        measurement,
        flaggedReadings,
      );

      /** Confirm values. */
      expect(data.datasets[0].data[0].y).toBeCloseTo(expectedAverage, 3);

      /** Confirm units on y-axis properly displayed based on selected measurement. */
      const tickLabel = options.scales?.['y']?.ticks?.callback?.call({} as Scale, 70, 0, []);

      expect(tickLabel).toContain(expectedUnit);
    },
  );

  it('shows axis titles and larger text in the expanded view', () => {
    const { options } = curingChartConfig(
      points,
      castAt,
      colors,
      'ambientTempF',
      flaggedReadings,
      16,
    );
    expect(options.scales?.['x']?.title?.display).toBe(true);
    expect(options.scales?.['y']?.title).toMatchObject({
      display: true,
      text: 'Ambient temperature (°F)',
    });
    expect(options.font).toMatchObject({ size: 16 });
  });

  it('keeps the compact strip on the page', () => {
    const { options } = curingChartConfig(points, castAt, colors, 'internalTempF', flaggedReadings);
    expect(options.scales?.['x']?.title?.display).toBe(false);
    expect(options.font).toMatchObject({ size: 12 });
  });

  it('gives moisture readings a minimum y-axis span of 4', () => {
    const range = yAxisRange('moisturePct', 2.5, 3.5);

    expect(range).toBeDefined();
    expect(range!.max - range!.min).toBe(4);
    expect(range!.min).toBeGreaterThanOrEqual(0);
    expect(range!.max).toBeLessThanOrEqual(100);
  });

  it.each([
    ['moisturePct', 2, 4, { min: 0, max: 4 }],
    ['relativeHumidityPct', 95, 99, { min: 90, max: 100 }],
    ['co2Ppm', 410, 430, { min: 400, max: 440 }],
    ['ambientTempF', 2, 4, { min: 0, max: 20 }],
  ] as const)(
    'centres %s readings from %d to %d in a minimum range',
    (measurement, low, high, expected) => {
      expect(yAxisRange(measurement, low, high)).toEqual(expected);
    },
  );

  it('displays moisture as percent of sample weight', () => {
    const { options } = curingChartConfig(
      points,
      castAt,
      colors,
      'moisturePct',
      flaggedReadings,
      16,
    );

    expect(options.scales?.['y']?.title).toMatchObject({
      display: true,
      text: 'Moisture (% of sample weight)',
    });
  });

  it('always keeps every reading inside the range', () => {
    // VM-SYN-003's internal temperature runs from about 69.5 to 84 °F; the old range cut off the low end.
    expect(yAxisRange('internalTempF', 69.5, 84)).toEqual({ min: 60, max: 90 });
    for (const batch of MOCK_BATCH_DETAILS.filter((b) => b.isSynthetic)) {
      for (const measurement of [
        'internalTempF',
        'ambientTempF',
        'moisturePct',
        'relativeHumidityPct',
        'resistivityOhmPerFt',
        'co2Ppm',
      ] as const) {
        const { options } = curingChartConfig(
          mockStrength(batch).curingPoints,
          batch.castAt,
          colors,
          measurement,
          flaggedReadings,
        );
        const y = options.scales?.['y'] as { min?: number; max?: number };
        if (y.min === undefined) continue;
        const values = mockStrength(batch).curingPoints.map((p) => p[measurement]);
        expect(Math.min(...values)).toBeGreaterThanOrEqual(y.min);
        expect(Math.max(...values)).toBeLessThanOrEqual(y.max!);
      }
    }
  });

  it('leaves the range to Chart.js when readings already spread wider', () => {
    expect(yAxisRange('internalTempF', 50, 85)).toBeUndefined();
  });
});
