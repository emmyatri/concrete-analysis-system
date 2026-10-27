import type { Plugin } from 'chart.js';

/** Hovered days are highlighted on both charts (M2-42, M2 hi-fi mock). */
export function chartHoverPlugin(
  getHoveredDay: () => number | null,
  reportHoveredDay: (day: number | null) => void,
): Plugin<'line'> {
  return {
    /** Receives the hovered day. */
    id: 'shared-chart-hover',
    afterEvent: (chart, args) => {
      if (!args.inChartArea) {
        if (getHoveredDay() !== null) {
          reportHoveredDay(null);
        }
        return;
      }

      /** Check both values for x-axis and mouse position. */
      const xAxis = chart.scales['x'];
      const mouseX = args.event.x;

      if (!xAxis || mouseX === null) {
        return;
      }

      /** Raw value for day; may be decimal. */
      const dayAtPointer = xAxis.getValueForPixel(mouseX);

      if (dayAtPointer === undefined) {
        return;
      }

      /** Convert to whole number for day. */
      const day = Math.round(dayAtPointer);

      if (day !== getHoveredDay()) {
        reportHoveredDay(day);
      }
    },

    /** Renders hovered day. */
    afterDatasetsDraw: (chart) => {
      const day = getHoveredDay();
      if (day === null) {
        return;
      }
      const xAxis = chart.scales['x'];
      if (!xAxis) {
        return;
      }

      const x = xAxis.getPixelForValue(day);
      const { ctx, chartArea } = chart;

      ctx.save();
      ctx.beginPath();
      ctx.moveTo(x, chartArea.top);
      ctx.lineTo(x, chartArea.bottom);
      ctx.strokeStyle = '#64748b';
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 4]);
      ctx.stroke();
      ctx.restore();
    },
  };
}
