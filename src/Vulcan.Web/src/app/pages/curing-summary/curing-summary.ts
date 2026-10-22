import { formatNumber } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';

import { CuringReading, Specimen } from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';

const HOUR_MS = 60 * 60 * 1000;
const DAYS = [1, 3] as const;

type ReadingField = Exclude<keyof CuringReading, 'id' | 'takenAt'>;

/** One reading with its cylinder's first reading, to measure change since cast. */
interface WindowReading {
  reading: CuringReading;
  first: CuringReading;
}

interface Row {
  label: string;
  /** Formats a value for display. */
  format: (value: number) => string;
  /** The day's value from that day's readings, or null without any. */
  value: (readings: WindowReading[]) => number | null;
  /** ValidationLimit key whose Warn range flags the value, for absolute values only. */
  limitKey?: string;
}

const average = (values: number[]) =>
  values.length === 0 ? null : values.reduce((sum, v) => sum + v, 0) / values.length;
const fixed = (value: number, digits: string) => formatNumber(value, 'en-US', digits);
const signed = (value: number, digits: string) =>
  `${value < 0 ? '−' : '+'}${fixed(Math.abs(value), digits)}`;

/** The average across every reading that day. */
const mean = (field: ReadingField) => (readings: WindowReading[]) =>
  average(readings.map((r) => r.reading[field]));

/** Each cylinder's latest reading that day minus its first reading, averaged across cylinders. */
const change = (field: ReadingField) => (readings: WindowReading[]) => {
  const latest = new Map<CuringReading, CuringReading>();
  for (const { reading, first } of readings) {
    const current = latest.get(first);
    if (!current || reading.takenAt > current.takenAt) latest.set(first, reading);
  }
  return average([...latest].map(([first, last]) => last[field] - first[field]));
};

const ROWS: Row[] = [
  {
    label: 'Internal temp (avg)',
    value: mean('internalTempF'),
    format: (v) => `${fixed(v, '1.1-1')} °F`,
    limitKey: 'Curing.InternalTempF',
  },
  {
    label: 'Temp rise since cast (peak)',
    value: (readings) =>
      readings.length === 0
        ? null
        : Math.max(...readings.map((r) => r.reading.internalTempF - r.first.internalTempF)),
    format: (v) => `${signed(v, '1.1-1')} °F`,
  },
  {
    label: 'Ambient temp (avg)',
    value: mean('ambientTempF'),
    format: (v) => `${fixed(v, '1.1-1')} °F`,
    limitKey: 'Curing.AmbientTempF',
  },
  {
    label: 'Moisture change',
    value: change('moisturePct'),
    format: (v) => `${signed(v, '1.1-1')}%`,
  },
  {
    label: 'Relative humidity (avg)',
    value: mean('relativeHumidityPct'),
    format: (v) => `${fixed(v, '1.1-1')}%`,
    limitKey: 'Curing.RelativeHumidityPct',
  },
  {
    label: 'Resistivity change',
    value: change('resistivityOhmPerFt'),
    format: (v) => `${signed(v, '1.1-1')} Ω/ft`,
  },
  {
    label: 'CO₂ (avg)',
    value: mean('co2Ppm'),
    format: (v) => `${fixed(v, '1.0-0')} ppm`,
    limitKey: 'Curing.Co2Ppm',
  },
];

/**
 * The "Day 1 / Day 3" card (SCRUM-175, mockup M2-01): what the curing readings looked like in the
 * first and third 24 hours after cast, across all cylinders. Values outside their Warn range are
 * flagged like the rest of Batch Detail.
 */
@Component({
  selector: 'app-curing-summary',
  templateUrl: './curing-summary.html',
  styleUrl: './curing-summary.scss',
})
export class CuringSummary {
  private readonly referenceData = inject(ReferenceDataService);

  readonly specimens = input.required<Specimen[]>();
  /** The batch's cast time (ISO, UTC). */
  readonly castAt = input.required<string>();

  protected readonly days = DAYS;

  protected readonly hasReadings = computed(() =>
    this.specimens().some((s) => s.curingReadings.length > 0),
  );

  protected readonly rows = computed(() => {
    const castAt = Date.parse(this.castAt());
    // Day N is from (N − 1) × 24 h to N × 24 h after cast, both ends included.
    const windows = DAYS.map((day) =>
      this.specimens().flatMap((specimen) => {
        const readings = specimen.curingReadings.filter((r) => Date.parse(r.takenAt) >= castAt);
        const first = readings[0];
        return readings
          .filter((r) => {
            const hours = (Date.parse(r.takenAt) - castAt) / HOUR_MS;
            return hours >= (day - 1) * 24 && hours <= day * 24;
          })
          .map((reading) => ({ reading, first }));
      }),
    );

    return ROWS.map((row) => ({
      label: row.label,
      cells: windows.map((readings) => {
        const value = row.value(readings);
        return {
          text: value === null ? '—' : row.format(value),
          warning: value === null || !row.limitKey ? null : this.warning(row.limitKey, value),
        };
      }),
    }));
  });

  /** The Warn limit's message when the value is outside its normal range. */
  private warning(limitKey: string, value: number): string | null {
    const warn = this.referenceData.limitFor(limitKey)?.warn;
    if (!warn) return null;
    const outside =
      (warn.min !== null && value < warn.min) || (warn.max !== null && value > warn.max);
    return outside ? warn.message : null;
  }
}
