import type { ChartData, ChartDataset, ChartOptions, ScriptableContext } from 'chart.js';
import { rightGutter } from '../../shared/chart-layout';
import { CuringPoint } from '../../core/api/models';

/** Theme values used by the curing chart. */
export interface CuringChartColors {
  text: string;
  grid: string;
  font: string;
  line: string;
  shade: string;
  flagged: string;
}

/** x-axis gridline positions; tick labels are hidden in the chart options. (M2-44, M2 hi-fi mock). */
const DAY_TICKS = [0, 1, 3, 7, 14, 21, 28];

/** One point on the chart. */
interface ChartPoint {
  x: number;
  y: number;
}

/** A line with markers illustrating min, max, average curing readings for a given day. */
type CuringDataset = ChartDataset<'line', ChartPoint[]>;

export interface CuringChartConfig {
  data: ChartData<'line', ChartPoint[]>;
  readingHours: number[];
  options: ChartOptions<'line'>;
}

/** Defines selectable curing measurements by excluding cylinder identifiers, timestamps, and
 * maturity from CuringPoint.
 * */
export type CuringMeasurement = Exclude<
  keyof CuringPoint,
  'specimenId' | 'specimenLabel' | 'time' | 'maturityFHours'
>;

/** Human-readable labels for the units on y-axis based on selected metric.*/
export const measurementLabels: Record<CuringMeasurement, string> = {
  internalTempF: 'Internal temperature (°F)',
  ambientTempF: 'Ambient temperature (°F)',
  moisturePct: 'Moisture (% of sample weight)',
  relativeHumidityPct: 'Relative humidity (%)',
  resistivityOhmPerFt: 'Resistivity (Ω/ft)',
  co2Ppm: 'CO₂ (ppm)',
};

/** The configuration instructions for the curing chart.*/
/**
 * Minimum y-axis span for each measurement.
 * Prevents small sensor fluctuations from excessively expanding the chart.
 * Moisture uses a 4-percentage-point span to accommodate its 2–4%
 * normal range without exaggerating minor variations.
 */
export const MIN_Y_SPAN: Record<CuringMeasurement, number> = {
  internalTempF: 20,
  ambientTempF: 20,
  moisturePct: 4,
  relativeHumidityPct: 10,
  resistivityOhmPerFt: 10,
  co2Ppm: 40,
};

/** Percent measurements can't go above 100. */
const PERCENT_MEASUREMENTS = new Set<CuringMeasurement>(['moisturePct', 'relativeHumidityPct']);

/**
 * A y-axis range at least MIN_Y_SPAN wide, centred on the readings. Undefined when the readings
 * already spread wider, so Chart.js picks the range as before.
 */
export function yAxisRange(
  measurement: CuringMeasurement,
  low: number,
  high: number,
): { min: number; max: number } | undefined {
  const span = MIN_Y_SPAN[measurement];
  if (!Number.isFinite(low) || !Number.isFinite(high) || high - low >= span) return undefined;
  // Snap outward to half-span steps so the gridlines land on round numbers (85–95%, 400–440 ppm),
  // then widen on the side with less room until the range is at least `span`. Every reading
  // always stays inside the range.
  const step = span / 2;
  let min = Math.floor(low / step) * step;
  let max = Math.ceil(high / step) * step;
  if (max === min) max += step;
  while (max - min < span) {
    if (low - min <= max - high) min -= step;
    else max += step;
  }
  const ceiling = PERCENT_MEASUREMENTS.has(measurement) ? 100 : Infinity;
  if (max > ceiling) {
    min -= max - ceiling;
    max = ceiling;
  }
  if (min < 0) {
    max -= min;
    min = 0;
  }
  return { min, max };
}

export function curingChartConfig(
  curingPoints: CuringPoint[],
  castAt: string,
  colors: CuringChartColors,
  selectedMeasurement: CuringMeasurement,
  flaggedReadings: ReadonlyMap<string, string>,
  /** Axis and tooltip text size. Larger in the expanded view, which also shows the axis titles. */
  fontSize = 12,
): CuringChartConfig {
  const expanded = fontSize > 12;
  const measurementLabel = measurementLabels[selectedMeasurement];
  const measurementUnits: Record<CuringMeasurement, string> = {
    internalTempF: ' °F',
    ambientTempF: ' °F',
    moisturePct: '%',
    relativeHumidityPct: '%',
    resistivityOhmPerFt: ' Ω/ft',
    co2Ppm: ' ppm',
  };

  const font = { family: colors.font, size: fontSize };

  /** Group curing points by timestamp.*/
  const readingsByTime = new Map<number, CuringPoint[]>();

  for (const point of curingPoints) {
    const timestamp = Date.parse(point.time);
    if (!readingsByTime.has(timestamp)) {
      readingsByTime.set(timestamp, []);
    }
    readingsByTime.get(timestamp)!.push(point);
  }

  /** Sort readings by timestamp. */
  const sortedTimes = [...readingsByTime.keys()].sort((a, b) => a - b);

  const averagePoints: ChartPoint[] = [];
  const minPoints: ChartPoint[] = [];
  const maxPoints: ChartPoint[] = [];

  for (const timestamp of sortedTimes) {
    const readings = readingsByTime.get(timestamp)!;

    /* Obtain average, min, max values for given measurement. */
    const values = readings.map((point) => point[selectedMeasurement]);
    const minValue = Math.min(...values);
    const maxValue = Math.max(...values);
    const avgValue = values.reduce((sum, value) => sum + value, 0) / values.length;

    const day = (timestamp - Date.parse(castAt)) / 86_400_000;

    /** Convert points to a curve for the chart rendering.*/
    averagePoints.push({ x: day, y: avgValue });
    minPoints.push({ x: day, y: minValue });
    maxPoints.push({ x: day, y: maxValue });
  }

  /** Check whether a particular reading has a warning, flag by color. */
  const markerColor = (context: ScriptableContext<'line'>) => {
    const timestamp = sortedTimes[context.dataIndex];
    const readingsAtTime = readingsByTime.get(timestamp) ?? [];
    const hasWarning = readingsAtTime.some((reading) =>
      flaggedReadings.has(`${reading.specimenLabel} | ${timestamp}`),
    );
    return hasWarning ? colors.flagged : colors.line;
  };

  /** Assemble each dataset and add to datasets.*/
  const datasets: CuringDataset[] = [];

  const averageCurve = {
    label: 'Average',
    data: averagePoints,
  };

  const minCurve = {
    label: 'Minimum',
    data: minPoints,
  };

  const maxCurve = {
    label: 'Maximum',
    data: maxPoints,
  };

  datasets.push({
    ...averageCurve,
    borderColor: colors.line,
    pointBackgroundColor: markerColor,
    pointBorderColor: markerColor,
    pointRadius: (context) => (markerColor(context) === colors.flagged ? 5 : 0),
    pointHoverRadius: 7,
  });

  datasets.push({
    ...maxCurve,
    borderWidth: 0,
    pointRadius: 0,
    pointHoverRadius: 0,
    fill: '+1',
    backgroundColor: colors.shade,
  });

  datasets.push({
    ...minCurve,
    borderWidth: 0,
    pointRadius: 0,
    pointHoverRadius: 0,
    fill: false,
  });

  /** Return the curing chart instructions + reading hours for caption (M2-44). */
  return {
    data: { datasets },
    readingHours: sortedTimes.map((timestamp) => (timestamp - Date.parse(castAt)) / 3_600_000),
    options: {
      maintainAspectRatio: false,
      animation: false,
      font,
      interaction: { mode: 'index', intersect: false },
      // On the page: the same right padding as the strength chart above, so the days line up.
      // The expanded view stands alone, so it uses the full width.
      layout: { padding: { top: 4, right: expanded ? 8 : rightGutter(fontSize) } },
      scales: {
        x: {
          type: 'linear',
          min: 0,
          max: 28,
          title: { display: expanded, text: 'Days since cast', color: colors.text, font },
          ticks: { display: expanded, color: colors.text, font },
          afterBuildTicks: (axis) => {
            axis.ticks = DAY_TICKS.map((value) => ({ value }));
          },
          grid: {
            drawTicks: false,
            color: (context) =>
              [1, 3].includes(context.tick?.value ?? -1) ? colors.text : colors.grid,
            lineWidth: (context) => ([1, 3].includes(context.tick?.value ?? -1) ? 1.5 : 1),
          },
        },
        y: {
          ...yAxisRange(
            selectedMeasurement,
            Math.min(...minPoints.map((point) => point.y)),
            Math.max(...maxPoints.map((point) => point.y)),
          ),
          title: { display: expanded, text: measurementLabel, color: colors.text, font },
          ticks: {
            maxTicksLimit: expanded ? 6 : 3,
            color: colors.text,
            font,
            callback: (value) =>
              `${Number(value).toLocaleString('en-US')}${measurementUnits[selectedMeasurement]}`,
          },
          grid: { color: colors.grid },
          // 70px lines the strip up with the strength chart above it; wider for larger text.
          afterFit: (scale) => {
            scale.width = 70 + (fontSize - 12) * 11;
          },
        },
      },
      plugins: {
        legend: {
          display: false,
        },
        tooltip: {
          bodyFont: font,
          filter: (item) => item.datasetIndex === 0,
          callbacks: {
            title: () => '',
            label: (item) => {
              const point = item.raw as ChartPoint;
              const timestamp = sortedTimes[item.dataIndex];
              /* The cylinders whose reading at this time was flagged for the selected measurement. */
              const flaggedAtTime = (readingsByTime.get(timestamp) ?? []).filter((reading) =>
                flaggedReadings.has(`${reading.specimenLabel} | ${timestamp}`),
              );
              /* Convert warnings to strings for tooltip rendering. */
              const warningLines = flaggedAtTime.map(
                (reading) =>
                  `${reading.specimenLabel}: ${reading[selectedMeasurement].toLocaleString('en-US')}${measurementUnits[selectedMeasurement]} ⚠ ${flaggedReadings.get(`${reading.specimenLabel} | ${timestamp}`)}`,
              );
              const day = Number.isInteger(point.x) ? point.x : point.x.toFixed(1);
              /* Tooltip message. */
              if (warningLines.length > 0) {
                return warningLines;
              }
              return `${item.dataset.label}: ${point.y.toLocaleString('en-US', { maximumFractionDigits: 1 })}${measurementUnits[selectedMeasurement]} at day ${day}`;
            },
          },
        },
      },
    },
  };
}
