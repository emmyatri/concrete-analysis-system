import type { ChartData, ChartDataset, ChartOptions, ScriptableContext } from 'chart.js';
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

/** x-axis gridline positions; tick labels are hidden in the chart options. (M2-42, M2 hi-fi mock). */
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
  moisturePct: 'Moisture (%)',
  relativeHumidityPct: 'Relative humidity (%)',
  resistivityOhmPerFt: 'Resistivity (Ω/ft)',
  co2Ppm: 'CO₂ (ppm)',
};

/** The configuration instructions for the curing chart.*/
export function curingChartConfig(
  curingPoints: CuringPoint[],
  castAt: string,
  colors: CuringChartColors,
  selectedMeasurement: CuringMeasurement,
  flaggedReadings: ReadonlyMap<string, string>,
): CuringChartConfig {
  const measurementLabel = measurementLabels[selectedMeasurement];
  const measurementUnits: Record<CuringMeasurement, string> = {
    internalTempF: ' °F',
    ambientTempF: ' °F',
    moisturePct: '%',
    relativeHumidityPct: '%',
    resistivityOhmPerFt: ' Ω/ft',
    co2Ppm: ' ppm',
  };

  const font = { family: colors.font, size: 12 };

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
    return selectedMeasurement === 'ambientTempF' && hasWarning ? colors.flagged : colors.line;
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

  /** Return the curing chart instructions + reading hours for caption (M2-42). */
  return {
    data: { datasets },
    readingHours: sortedTimes.map((timestamp) => (timestamp - Date.parse(castAt)) / 3_600_000),
    options: {
      maintainAspectRatio: false,
      animation: false,
      font,
      interaction: { mode: 'nearest', intersect: false },
      layout: { padding: { top: 4, right: 8 } },
      scales: {
        x: {
          type: 'linear',
          min: 0,
          max: 28,
          title: { display: false, text: 'Days since cast', color: colors.text, font },
          ticks: { display: false, color: colors.text, font },
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
          title: { display: false, text: measurementLabel, color: colors.text, font },
          ticks: {
            maxTicksLimit: 3,
            color: colors.text,
            font,
            callback: (value) =>
              `${Number(value).toLocaleString('en-US')}${measurementUnits[selectedMeasurement]}`,
          },
          grid: { color: colors.grid },
          afterFit: (scale) => {
            scale.width = 70;
          },
        },
      },
      plugins: {
        legend: {
          display: false,
        },
        tooltip: {
          filter: (item) => item.datasetIndex === 0,
          callbacks: {
            title: () => '',
            label: (item) => {
              const point = item.raw as ChartPoint;
              const timestamp = sortedTimes[item.dataIndex];
              /* Using the point's timestamp, determine if warnings exist when measurement is ambient Temp. */
              const flaggedAtTime =
                selectedMeasurement === 'ambientTempF'
                  ? (readingsByTime.get(timestamp) ?? []).filter((reading) =>
                      flaggedReadings.has(`${reading.specimenLabel} | ${timestamp}`),
                    )
                  : [];
              /* Convert warnings to strings for tooltip rendering. */
              const warningLines = flaggedAtTime.map(
                (reading) =>
                  `${reading.specimenLabel}: ${reading.ambientTempF.toFixed(1)}
°F — ${flaggedReadings.get(`${reading.specimenLabel} | ${timestamp}`)}`,
              );
              const day = Number.isInteger(point.x) ? point.x : point.x.toFixed(1);
              /* Tooltip message. */
              if (warningLines.length > 0) {
                return warningLines;
              }
              return `${item.dataset.label}: ${point.y.toLocaleString('en-US')}${measurementUnits[selectedMeasurement]} at day ${day}`;
            },
          },
        },
      },
    },
  };
}
