import type { ChartData, ChartDataset, ChartOptions, Plugin } from 'chart.js';

import { BatchStrength } from '../../core/api/models';

/** Colors for the chart, read from the theme's PrimeNG tokens (StrengthChart.colors). */
export interface StrengthChartColors {
  predicted: string;
  initialEstimate: string;
  breaks: string;
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

/** A line or set of dots. endLabel is drawn at the right end of the line by endLabelsPlugin. */
type StrengthDataset = ChartDataset<'line', ChartPoint[]> & { endLabel?: string };

export interface StrengthChartConfig {
  data: ChartData<'line', ChartPoint[]>;
  options: ChartOptions<'line'>;
}

/** The mockup's x-axis: cast, the three break ages, and each week. */
const DAY_TICKS = [0, 3, 7, 14, 21, 28];

/**
 * Everything p-chart needs to draw one batch (SCRUM-174, mockup M2-01): the predicted curve, the
 * curve before breaks, the 3/7/28-day estimates as hollow markers, breaks as dots (flagged ones in orange) with a line through their mean at each
 * age, and the Expected final and minimum as reference lines. Datasets are labeled by batch number.
 */
export function strengthChartConfig(
  strength: BatchStrength,
  minimumPsi: number | null,
  flaggedSpecimenIds: ReadonlySet<number>,
  colors: StrengthChartColors,
): StrengthChartConfig {
  const batch = strength.batchNumber;
  const curve = (points: BatchStrength['curvePoints']): ChartPoint[] => [
    { x: 0, y: 0 },
    ...points.map((p) => ({ x: p.day, y: p.psi })),
  ];
  const datasets: StrengthDataset[] = [];

  if (strength.curvePoints.length > 0) {
    datasets.push({
      label: `${batch} predicted`,
      data: curve(strength.curvePoints),
      borderColor: colors.predicted,
      backgroundColor: colors.predicted,
      borderWidth: 2.5,
      pointRadius: 0,
      pointStyle: 'line',
      endLabel:
        strength.projected28DayPsi === null
          ? undefined
          : `28-day est. ${psi(strength.projected28DayPsi)}`,
    });
  }
  if (strength.unadjustedCurvePoints.length > 0) {
    datasets.push({
      label: `${batch} before breaks`,
      data: curve(strength.unadjustedCurvePoints),
      borderColor: colors.initialEstimate,
      backgroundColor: colors.initialEstimate,
      borderWidth: 1.5,
      borderDash: [4, 4],
      pointRadius: 0,
      pointStyle: 'line',
    });
  }

  // SCRUM-175: the 3-, 7- and 28-day estimates as hollow markers, so they can't be mistaken for breaks.
  const estimates = strength.estimates.filter((e) => e.estimatedPsi !== null);
  if (estimates.length > 0) {
    datasets.push({
      label: `${batch} estimate`,
      data: estimates.map((e) => ({ x: e.horizonDays, y: e.estimatedPsi! })),
      borderColor: colors.predicted,
      backgroundColor: colors.surface,
      borderWidth: 2,
      showLine: false,
      pointRadius: 6,
      pointHoverRadius: 8,
      pointStyle: 'circle',
    });
  }

  const breaks = strength.measuredBreaks.map((b) => ({
    x: b.ageDays,
    y: b.psi,
    specimen: b.specimenLabel,
    flagged: flaggedSpecimenIds.has(b.specimenId),
  }));
  const dots = (label: string, color: string, points: typeof breaks): StrengthDataset => ({
    label,
    data: points.map(({ x, y, specimen }) => ({ x, y, specimen })),
    borderColor: color,
    backgroundColor: color,
    showLine: false,
    pointRadius: 5,
    pointHoverRadius: 7,
    pointStyle: 'circle',
  });
  if (breaks.some((b) => !b.flagged)) {
    datasets.push(
      dots(
        `${batch} breaks`,
        colors.breaks,
        breaks.filter((b) => !b.flagged),
      ),
    );
  }
  if (breaks.some((b) => b.flagged)) {
    datasets.push(
      dots(
        `${batch} flagged breaks`,
        colors.flagged,
        breaks.filter((b) => b.flagged),
      ),
    );
  }
  if (breaks.length > 0) {
    datasets.push({
      label: `${batch} mean by age`,
      data: meanByAge(breaks),
      borderColor: colors.breaks,
      backgroundColor: colors.breaks,
      borderWidth: 2,
      pointRadius: 0,
      pointStyle: 'line',
    });
  }

  /** A flat line across all 28 days, e.g. the Expected final. */
  const reference = (
    label: string,
    endLabel: string,
    y: number,
    color: string,
    dash: number[],
  ): StrengthDataset => ({
    label,
    data: [
      { x: 0, y },
      { x: 28, y },
    ],
    borderColor: color,
    backgroundColor: color,
    borderWidth: 1.5,
    borderDash: dash,
    pointRadius: 0,
    pointHoverRadius: 0,
    pointStyle: 'line',
    endLabel,
  });
  const expected = strength.expectedFinalBreakPsi;
  datasets.push(
    reference('Expected final', `Expected final ${psi(expected)}`, expected, colors.target, [6, 4]),
  );
  if (minimumPsi !== null) {
    datasets.push(
      reference('Minimum', `${psi(minimumPsi)} min`, minimumPsi, colors.minimum, [2, 3]),
    );
  }

  const highest = Math.max(...datasets.flatMap((d) => d.data.map((p) => p.y)));
  const step = highest > 5000 ? 2000 : 1000;
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
          min: 0,
          // Fits the highest of the curves, breaks and Expected final, rounded up to the next gridline.
          max: Math.ceil((highest * 1.08) / step) * step,
          title: { display: true, text: 'Strength (PSI)', color: colors.text, font },
          ticks: {
            stepSize: step,
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
              return point.specimen
                ? `${point.specimen}: ${psi(point.y)} PSI at day ${day}`
                : `${item.dataset.label}: ${psi(point.y)} PSI at day ${day}`;
            },
          },
        },
      },
    },
  };
}

/** The average break at each test age, joined into a line. */
function meanByAge(breaks: { x: number; y: number }[]): ChartPoint[] {
  const byAge = new Map<number, { x: number[]; y: number[] }>();
  for (const b of breaks) {
    const group = byAge.get(Math.round(b.x)) ?? { x: [], y: [] };
    group.x.push(b.x);
    group.y.push(b.y);
    byAge.set(Math.round(b.x), group);
  }
  return [...byAge.values()]
    .map((group) => ({ x: average(group.x), y: Math.round(average(group.y)) }))
    .sort((a, b) => a.x - b.x);
}

/**
 * Draws each dataset's endLabel at the right end of its line, above it. When two lines end close
 * together (a batch right on its Expected final), the lower label moves below its line instead.
 */
export const endLabelsPlugin: Plugin<'line'> = {
  id: 'vulcanEndLabels',
  afterDatasetsDraw(chart) {
    const { ctx, chartArea } = chart;
    const lineHeight = 13;
    const labels = chart.data.datasets
      .map((dataset, index) => ({
        text: (dataset as StrengthDataset).endLabel,
        color: String(dataset.borderColor),
        y: chart.getDatasetMeta(index).data.at(-1)?.y,
        visible: chart.isDatasetVisible(index),
      }))
      .filter((l): l is { text: string; color: string; y: number; visible: true } =>
        Boolean(l.text && l.y !== undefined && l.visible),
      )
      .sort((a, b) => a.y - b.y);

    ctx.save();
    ctx.font = `600 11px ${chart.options.font?.family ?? 'sans-serif'}`;
    ctx.textAlign = 'right';
    ctx.textBaseline = 'bottom';
    let previous = -Infinity;
    for (const label of labels) {
      let bottom = label.y - 5;
      if (bottom - lineHeight < previous)
        bottom = Math.max(label.y + 5 + lineHeight, previous + lineHeight);
      ctx.fillStyle = label.color;
      ctx.fillText(label.text, chartArea.right - 4, bottom);
      previous = bottom;
    }
    ctx.restore();
  },
};

function average(values: number[]): number {
  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

/** 6200 → "6,200" */
function psi(value: number): string {
  return value.toLocaleString('en-US');
}
