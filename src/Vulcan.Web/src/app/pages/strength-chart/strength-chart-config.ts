import type { ChartData, ChartDataset, ChartOptions, Plugin } from 'chart.js';

import { BatchStrength } from '../../core/api/models';
import { rightGutter } from '../../shared/chart-layout';

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
 * curve before breaks, the 3/7/28-day estimates as hollow markers, breaks as dots (flagged ones in
 * orange) with a dash at their mean at each age, and the Expected final and minimum as reference
 * lines. The card title names the batch, so the legend doesn't repeat it.
 */
export function strengthChartConfig(
  strength: BatchStrength,
  minimumPsi: number | null,
  flaggedSpecimenIds: ReadonlySet<number>,
  colors: StrengthChartColors,
  /** Axis, legend and tooltip text size. Larger in the expanded view. */
  fontSize = 12,
): StrengthChartConfig {
  const curve = (points: BatchStrength['curvePoints']): ChartPoint[] => [
    { x: 0, y: 0 },
    ...points.map((p) => ({ x: p.day, y: p.psi })),
  ];
  const datasets: StrengthDataset[] = [];

  if (strength.curvePoints.length > 0) {
    datasets.push({
      label: 'Predicted',
      data: curve(strength.curvePoints),
      borderColor: colors.predicted,
      backgroundColor: colors.predicted,
      borderWidth: 2.5,
      pointRadius: 0,
      pointStyle: 'line',
      endLabel:
        strength.projected28DayPsi === null
          ? undefined
          : `28-day est. ${psi(strength.projected28DayPsi)}${versusTarget(strength)}`,
    });
  }
  if (strength.unadjustedCurvePoints.length > 0) {
    datasets.push({
      label: 'Before breaks',
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
      label: 'Estimate',
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
        'Breaks',
        colors.breaks,
        breaks.filter((b) => !b.flagged),
      ),
    );
  }
  if (breaks.some((b) => b.flagged)) {
    datasets.push(
      dots(
        'Flagged breaks',
        colors.flagged,
        breaks.filter((b) => b.flagged),
      ),
    );
  }
  if (breaks.length > 0) {
    // A dash at the average break for each age. Not joined into a line: between test ages it
    // would only be a straight line between averages, which reads like a third prediction.
    const means = meanByAge(breaks);
    const last = means.at(-1);
    datasets.push({
      label: 'Mean by age',
      data: means,
      borderColor: colors.breaks,
      backgroundColor: colors.breaks,
      borderWidth: 3,
      showLine: false,
      pointRadius: 9,
      pointHoverRadius: 11,
      pointStyle: 'line',
      endLabel: last && Math.round(last.x) === 28 ? `28-day actual ${psi(last.y)}` : undefined,
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
  const font = { family: colors.font, size: fontSize };

  return {
    data: { datasets },
    options: {
      maintainAspectRatio: false,
      animation: false,
      font,
      interaction: { mode: 'nearest', intersect: false },
      // The end labels sit in this right-hand gutter, clear of the lines and markers at day 28.
      layout: { padding: { top: 4, right: rightGutter(fontSize) } },
      clip: 8,
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
          // 70px lines the plot up with the curing strip below it; wider for larger text.
          afterFit: (scale) => {
            scale.width = 70 + (fontSize - 12) * 11;
          },
        },
      },
      plugins: {
        legend: {
          position: 'top',
          align: 'end',
          labels: { usePointStyle: true, boxHeight: 6, color: colors.text, font },
        },
        tooltip: {
          bodyFont: font,
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

/** "(−10%)" below the Expected final, "(+3%)" above it; nothing without a projection. */
function versusTarget(strength: BatchStrength): string {
  if (strength.shortfallPct === null || strength.projected28DayPsi === null) return '';
  const pct = Math.round(Math.abs(strength.shortfallPct));
  if (pct === 0) return ' (on target)';
  return strength.shortfallPct > 0 ? ` (−${pct}%)` : ` (+${pct}%)`;
}

/** The average break at each test age, one point per age. */
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
 * Draws each dataset's endLabel in the gutter to the right of the plot, level with the end of its
 * line or its last marker. Labels that would overlap are pushed apart, top to bottom.
 */
export const endLabelsPlugin: Plugin<'line'> = {
  id: 'vulcanEndLabels',
  afterDatasetsDraw(chart) {
    const { ctx, chartArea } = chart;
    // End labels scale with the chart's font (11px at the default 12px).
    const labelSize = Math.round(
      ((chart.options.font?.size as number | undefined) ?? 12) * (11 / 12),
    );
    const lineHeight = labelSize + 2;
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
    ctx.font = `600 ${labelSize}px ${chart.options.font?.family ?? 'sans-serif'}`;
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';
    let previous = -Infinity;
    for (const label of labels) {
      const y = Math.min(Math.max(label.y, previous + lineHeight), chartArea.bottom);
      ctx.fillStyle = label.color;
      ctx.fillText(label.text, chartArea.right + 10, y);
      previous = y;
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
