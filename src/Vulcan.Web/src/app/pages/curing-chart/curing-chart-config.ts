import type { ChartData, ChartDataset, ChartOptions, Plugin } from 'chart.js';
import {CuringPoint} from '../../core/api/models';


/** Colors for the chart, read from the theme's PrimeNG tokens (CuringChart.colors). */
export interface CuringChartColors {
  flagged: string;
  target: string;
  minimum: string;
  text: string;
  grid: string;
  /** The card background, for the inside of the hollow estimate markers. */
  surface: string;
  font: string;
}

/** One point on the chart. Breaks also carry their cylinder label for the tooltip. */
interface ChartPoint {
  x: number;
  y: number;
  specimen?: string;
}

export type CuringMeasurement = Exclude<
  keyof CuringPoint,
  'specimenId' | 'specimenLabel' | 'time' | 'maturityFHours'
>;

/** A line or set of dots. endLabel is drawn at the right end of the line by endLabelsPlugin. */
type CuringDataset = ChartDataset<'line', ChartPoint[]> & { endLabel?: string };

export interface CuringChartConfig {
  data: ChartData<'line', ChartPoint[]>;
  options: ChartOptions<'line'>;
}

/** The mockup's x-axis: cast, the three break ages, and each week. */
const DAY_TICKS = [0, 3, 7, 14, 21, 28];

export function curingChartConfig(
  curingPoints: CuringPoint[],
  castAt: string,
  colors: CuringChartColors,
  selectedMeasurement: CuringMeasurement,
): CuringChartConfig {
  /* TODO: const yAxis = set to selected measurement*/
  /* TODO: graph label*/

  const curve = (points:CuringPoint[]): ChartPoint[] =>
      points.map((p) => ({
      x: (Date.parse(p.time) - Date.parse(castAt)) / 86_400_000,
      y: p[selectedMeasurement]}));

  /** Group readings by cylinder.*/
  const readingsBySpecimen = new Map<number, CuringPoint[]>();

  for (const point of curingPoints) {
    if (!readingsBySpecimen.has(point.specimenId)) {
      readingsBySpecimen.set(point.specimenId, []);
    }

    readingsBySpecimen.get(point.specimenId)!.push(point);
  }

  /** Add each cylinder + respective readings as a dataset to datasets*/
  const datasets: CuringDataset[] = [];

  const cylinderStyle: Partial<CuringDataset> = {
    /** TODO: Colors */
    showLine: true,
    pointRadius: 5,
    pointHoverRadius: 7,
    pointStyle: 'circle',
  };

  for (const readings of readingsBySpecimen.values()) {
    const firstReading = readings[0];
    if (!firstReading) {
      continue
    }

    const chartPoints = curve(readings);
    const specimenLabel = firstReading.specimenLabel

    const cylinderData = {
      label: specimenLabel,
      data: chartPoints,
    }

    datasets.push({
      ...cylinderStyle,
      ...cylinderData,
    });
  }

  /** Determine tick distance (step size) and max value for y-axis for
   * each measurement in the selector.
   */

  const highest = datasets.length > 0
    ? Math.max(...datasets.flatMap((d) => d.data.map((p) => p.y))): 0;
  const lowest = datasets.length > 0
    ? Math.min(...datasets.flatMap((d) => d.data.map((p) => p.y))): 0;


  /* TODO: const step = contingent on selected measurement*/
  const font = { family: colors.font, size: 12 };

  return {
    data: { datasets },
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
          title: { display: true, text: 'Days since cast', color: colors.text, font },
          ticks: { color: colors.text, font },
          afterBuildTicks: (axis) => {
            axis.ticks = DAY_TICKS.map((value) => ({ value }));
          },
          grid: { color: colors.grid },
        },
        y: {
          title: { display: true, text: '{selected metric}', color: colors.text, font },
          ticks: {
            maxTicksLimit: 6,
            color: colors.text,
            font,
            callback: (value) => Number(value).toLocaleString('en-US'),
          },
          grid: { color: colors.grid },
        },
      },
      plugins: {
        legend: {
          position: 'top',
          align: 'end',
          labels: { usePointStyle: true, boxHeight: 6, color: colors.text, font },
        },
        tooltip: {
          callbacks: {
            title: () => '',
            label: (item) => {
              const point = item.raw as ChartPoint;
              const day = Number.isInteger(point.x) ? point.x : point.x.toFixed(1);
              return `${item.dataset.label}: ${point.y.toLocaleString('en-US')} {selected metric} at day ${day}`;
            },
          },
        },
      },
    },
  };
}





