import type { ChartData, ChartDataset, ChartOptions } from 'chart.js';
import {CuringPoint} from '../../core/api/models';


/** Theme values used by the curing chart. */
export interface CuringChartColors {
  text: string;
  grid: string;
  font: string;
  cylinders: readonly string[];
}

/** One point on the chart. */
interface ChartPoint {
  x: number;
  y: number;
}

export type CuringMeasurement = Exclude<
  keyof CuringPoint,
  'specimenId' | 'specimenLabel' | 'time' | 'maturityFHours'
>;

/** A line with markers illustrating curing readings for a cylinder. */
type CuringDataset = ChartDataset<'line', ChartPoint[]>;

export interface CuringChartConfig {
  data: ChartData<'line', ChartPoint[]>;
  options: ChartOptions<'line'>;
}

/** The mockup's x-axis matches break strength. */
const DAY_TICKS = [0, 3, 7, 14, 21, 28];

/** Distinguishable line markers for charted curing readings. */
const CYLINDER_MARKERS = [
  'circle',
  'rect',
  'triangle',
  'rectRot',
] as const;

/** Assigns a color and marker combination to a cylinder. */
export function cylinderAppearance(index: number, colors: readonly string[]) {
  const color = colors[index % colors.length];
  const marker =
    CYLINDER_MARKERS[Math.floor(index / colors.length) % CYLINDER_MARKERS.length];

  return {
    borderColor: color,
    backgroundColor: color,
    pointStyle: marker,
  };
}


/** Human-readable labels for the units on y-axis based on selected metric.*/
export const measurementLabels: Record<CuringMeasurement, string> = {
  internalTempF: 'Internal temperature (°F)',
  ambientTempF: 'Ambient temperature (°F)',
  moisturePct: 'Moisture (%)',
  relativeHumidityPct: 'Relative humidity (%)',
  resistivityOhmPerFt: 'Resistivity (Ω/ft)',
  co2Ppm: 'CO₂ (ppm)',
};


export function curingChartConfig(
  curingPoints: CuringPoint[],
  castAt: string,
  colors: CuringChartColors,
  selectedMeasurement: CuringMeasurement,
  hiddenCylinderIds: ReadonlySet<number>,
): CuringChartConfig {

  const measurementLabel = measurementLabels[selectedMeasurement];

  const curve = (points:CuringPoint[]): ChartPoint[] =>
      points.map((p) => ({
      x: (Date.parse(p.time) - Date.parse(castAt)) / 86_400_000,
      y: p[selectedMeasurement]}));

  /** Group curing points by cylinder.*/
  const readingsBySpecimen = new Map<number, CuringPoint[]>();

  for (const point of curingPoints) {
    if (!readingsBySpecimen.has(point.specimenId)) {
      readingsBySpecimen.set(point.specimenId, []);
    }

    readingsBySpecimen.get(point.specimenId)!.push(point);
  }

  /** Add each cylinder + points as a dataset to datasets*/
  const datasets: CuringDataset[] = [];

  const cylinderStyle: Partial<CuringDataset> = {
    showLine: true,
    pointRadius: 5,
    pointHoverRadius: 7,
  };

  const sortedSpecimenIds = [...readingsBySpecimen.keys()].sort((a, b) => a - b);

  for (const [index, specimenId] of sortedSpecimenIds.entries()) {
    const readings = readingsBySpecimen.get(specimenId)!;
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
      ...cylinderAppearance(index, colors.cylinders),
      hidden: hiddenCylinderIds.has(specimenId),
    });
  }

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
          title: { display: true, text: measurementLabel, color: colors.text, font },
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
          display: false,
        },
        tooltip: {
          callbacks: {
            title: () => '',
            label: (item) => {
              const point = item.raw as ChartPoint;
              const day = Number.isInteger(point.x) ? point.x : point.x.toFixed(1);
              return `${item.dataset.label} | Day ${day} | ${measurementLabel}: ${point.y.toLocaleString('en-US')}`;
            },
          },
        },
      },
    },
  };
}





