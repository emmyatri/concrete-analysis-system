import { DatePipe, DecimalPipe, formatNumber } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { catchError, map, of, startWith, switchMap } from 'rxjs';

import { describeApiError } from '../../core/api/api-errors';
import {
  BatchDetail,
  CreateCuringReadingRequest,
  CuringReading,
  Specimen,
  ValidationLimit,
} from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { SyntheticDataSetting } from '../../core/session/synthetic-data';
import { parseNumber } from '../../core/validation/parse-number';
import { ValidationService } from '../../core/validation/validation.service';

type Field =
  | 'internalTempF'
  | 'ambientTempF'
  | 'moisturePct'
  | 'relativeHumidityPct'
  | 'resistivityOhmPerFt'
  | 'co2Ppm';

/**
 * The six measurements, in grid order, with the ValidationLimit key that checks each. `digits` and
 * `unit` are how a saved value reads, the same as on Batch Detail ("78.4 °F", "3.0%").
 */
export const COLUMNS: {
  field: Field;
  key: string;
  header: string;
  name: string;
  digits: string;
  unit: string;
  maxDecimals?: number;
}[] = [
  {
    field: 'internalTempF',
    key: 'Curing.InternalTempF',
    header: 'Internal (°F)',
    name: 'Internal',
    digits: '1.1-1',
    unit: ' °F',
    maxDecimals: 1,
  },
  {
    field: 'ambientTempF',
    key: 'Curing.AmbientTempF',
    header: 'Ambient (°F)',
    name: 'Ambient',
    digits: '1.1-1',
    unit: ' °F',
  },
  {
    field: 'moisturePct',
    key: 'Curing.MoisturePct',
    header: 'Moisture (% of sample weight)',
    name: 'Moisture (% of sample weight)',
    digits: '1.1-2',
    unit: '%',
  },
  {
    field: 'relativeHumidityPct',
    key: 'Curing.RelativeHumidityPct',
    header: 'RH (%)',
    name: 'RH',
    digits: '1.1-2',
    unit: '%',
  },
  {
    field: 'resistivityOhmPerFt',
    key: 'Curing.ResistivityOhmPerFt',
    header: 'Resistivity (Ω/ft)',
    name: 'Resistivity',
    digits: '1.2-2',
    unit: ' Ω/ft',
  },
  {
    field: 'co2Ppm',
    key: 'Curing.Co2Ppm',
    header: 'CO₂ (ppm)',
    name: 'CO₂',
    digits: '1.1-1',
    unit: ' ppm',
  },
];

/** One problem in a row: an error blocks saving it, a warning saves and is flagged. */
export interface Issue {
  field: Field | 'takenAt';
  severity: 'error' | 'warn';
  text: string;
}

type RowStatus = 'editing' | 'saving' | 'saved' | 'flagged' | 'failed';

let nextRowId = 1;

/** A reading being typed in. Each row saves on its own, so one bad row never blocks the others. */
export class NewRow {
  readonly id = nextRowId++;
  readonly takenAt = signal(toLocalInput(new Date()));
  readonly values = signal<Record<Field, string>>({
    internalTempF: '',
    ambientTempF: '',
    moisturePct: '',
    relativeHumidityPct: '',
    resistivityOhmPerFt: '',
    co2Ppm: '',
  });
  readonly status = signal<RowStatus>('editing');
  /** Set by the first Save, so a missing value only turns red once the operator has tried to save. */
  readonly attempted = signal(false);
  readonly serverIssues = signal<Issue[]>([]);
  /** Saved rows (with or without a flag) can't be edited here. */
  readonly locked = computed(() => ['saving', 'saved', 'flagged'].includes(this.status()));
}

/** "2026-10-06T14:05", the local time a datetime-local input shows. */
export function toLocalInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

const STATUS_LABELS: Record<RowStatus, string> = {
  editing: '',
  saving: 'Saving…',
  saved: 'Saved',
  flagged: 'Saved · flagged',
  failed: 'Not saved',
};

type Detail =
  | { status: 'none' }
  | { status: 'loading' }
  | { status: 'loaded'; batch: BatchDetail }
  | { status: 'failed'; message: string };

/**
 * Enter curing readings quickly (M1-63, mockup "06 · Curing readings"): pick a batch and cylinder, then
 * type readings into a grid. Tab moves across, Enter saves the row. Existing readings show oldest first
 * with the new row at the bottom. The batch and cylinder live in the URL (?batch=&cylinder=), so Batch
 * Detail can link straight to one cylinder.
 */
@Component({
  selector: 'app-curing-readings',
  imports: [DatePipe, DecimalPipe, FormsModule, RouterLink, ButtonModule, SelectModule],
  templateUrl: './curing-readings.html',
  styleUrl: './curing-readings.scss',
})
export class CuringReadingsPage {
  private readonly api = inject(VulcanApiService);
  private readonly router = inject(Router);
  private readonly referenceData = inject(ReferenceDataService);
  private readonly validation = inject(ValidationService);
  private readonly synthetic = inject(SyntheticDataSetting);

  /** ?batch=VM-SYN-006 */
  readonly batch = input<string>();
  /** ?cylinder=D3-1. Without it, the batch's first cylinder. */
  readonly cylinder = input<string>();

  protected readonly columns = COLUMNS;
  protected readonly statusLabels = STATUS_LABELS;

  protected readonly batchOptions = toSignal(
    toObservable(this.synthetic.show).pipe(
      switchMap((includeSynthetic) =>
        this.api.getBatches({ includeSynthetic }).pipe(
          map((batches) => batches.map((b) => b.batchNumber)),
          catchError(() => of<string[]>([])),
        ),
      ),
    ),
    { initialValue: [] as string[] },
  );

  protected readonly detail = toSignal(
    toObservable(this.batch).pipe(
      switchMap((batchNumber) => {
        if (!batchNumber) return of<Detail>({ status: 'none' });
        return this.api.getBatch(batchNumber).pipe(
          map((batch): Detail => ({ status: 'loaded', batch })),
          catchError((error: unknown) =>
            of<Detail>({ status: 'failed', message: describeApiError(error).message }),
          ),
          startWith<Detail>({ status: 'loading' }),
        );
      }),
    ),
    { initialValue: { status: 'none' } as Detail },
  );

  protected readonly loadedBatch = computed(() => {
    const detail = this.detail();
    return detail.status === 'loaded' ? detail.batch : null;
  });

  protected readonly cylinderOptions = computed(
    () => this.loadedBatch()?.specimens.map((s) => s.label) ?? [],
  );

  protected readonly specimen = computed((): Specimen | null => {
    const batch = this.loadedBatch();
    if (!batch || batch.specimens.length === 0) return null;
    const wanted = this.cylinder()?.toLowerCase();
    return batch.specimens.find((s) => s.label.toLowerCase() === wanted) ?? batch.specimens[0];
  });

  /** Readings already saved for this cylinder, oldest first, with any flags saved against them. */
  protected readonly existing = computed(() => {
    const batch = this.loadedBatch();
    const specimen = this.specimen();
    if (!batch || !specimen) return [];
    return [...specimen.curingReadings]
      .sort((a, b) => a.takenAt.localeCompare(b.takenAt))
      .map((reading) => {
        const flags = batch.warnings.filter(
          (w) => w.entityType === 'CuringReading' && w.entityId === reading.id,
        );
        return { reading, flagged: new Set(flags.map((w) => w.limitKey)) };
      });
  });

  protected readonly rows = signal<NewRow[]>([]);

  /** "Internal 0–200 °F · Ambient 0–150 °F (normal 60–80) · …" from ValidationLimit. */
  protected readonly ranges = computed(() =>
    COLUMNS.map((column) => {
      const { reject, warn } = this.limitsFor(column.key);
      if (!reject) return null;
      const normal = warn ? ` (normal ${formatRange(warn, false)})` : '';
      return `${column.name} ${formatRange(reject, true)}${normal}`;
    })
      .filter((text): text is string => text !== null)
      .join(' · '),
  );

  /** "Row 7 · Ambient 720 °F — must be 0–150 °F." for every row with something to say. */
  protected readonly messages = computed(() => {
    const offset = this.existing().length;
    return this.rows().flatMap((row, index) => {
      const shown = this.issuesFor(row).filter(
        (issue) =>
          (issue.severity === 'error' && row.status() === 'failed') ||
          (issue.severity === 'warn' && row.status() === 'flagged'),
      );
      return shown.map((issue) => ({
        severity: issue.severity,
        text: `Row ${offset + index + 1} · ${issue.text}`,
      }));
    });
  });

  protected readonly unsaved = computed(() => this.rows().filter((row) => !row.locked()).length);

  constructor() {
    // A different cylinder starts over with one empty row.
    effect(() => {
      this.specimen();
      this.rows.set([new NewRow()]);
    });
  }

  protected chooseBatch(batchNumber: string | null): void {
    this.router.navigate([], {
      queryParams: { batch: batchNumber || null, cylinder: null },
      replaceUrl: true,
    });
  }

  protected chooseCylinder(label: string | null): void {
    this.router.navigate([], {
      queryParams: { batch: this.batch(), cylinder: label || null },
      replaceUrl: true,
    });
  }

  protected addRow(focus = true): void {
    const row = new NewRow();
    this.rows.update((rows) => [...rows, row]);
    if (focus) setTimeout(() => document.getElementById(cellId(row, 'internalTempF'))?.focus());
  }

  protected setTakenAt(row: NewRow, value: string): void {
    row.takenAt.set(value);
    row.serverIssues.set([]);
  }

  protected setValue(row: NewRow, field: Field, value: string): void {
    row.values.update((values) => ({ ...values, [field]: value }));
    row.serverIssues.set(row.serverIssues().filter((issue) => issue.field !== field));
  }

  protected cellId(row: NewRow, field: Field | 'takenAt'): string {
    return cellId(row, field);
  }

  /** Everything wrong with a row right now, checked the same way the server checks it. */
  protected issuesFor(row: NewRow): Issue[] {
    const issues: Issue[] = [];
    const batch = this.loadedBatch();

    const text = row.takenAt();
    const takenAt = text ? new Date(text) : null;
    if (!takenAt || Number.isNaN(takenAt.getTime())) {
      issues.push({ field: 'takenAt', severity: 'error', text: 'Time taken — required.' });
    } else if (batch && takenAt < new Date(batch.castAt)) {
      issues.push({
        field: 'takenAt',
        severity: 'error',
        text: "Time taken — can't be before the batch was cast.",
      });
    } else if (takenAt.getTime() > Date.now()) {
      issues.push({
        field: 'takenAt',
        severity: 'error',
        text: "Time taken — can't be in the future.",
      });
    }

    const values = row.values();
    for (const column of COLUMNS) {
      const raw = values[column.field].trim();
      if (!raw) {
        issues.push({ field: column.field, severity: 'error', text: `${column.name} — required.` });
        continue;
      }
      const value = parseNumber(raw);
      if (value === null) {
        issues.push({
          field: column.field,
          severity: 'error',
          text: `${column.name} — enter a number.`,
        });
        continue;
      }
      if (column.maxDecimals !== undefined) {
        const decimalPart = raw.split('.')[1];

        if (decimalPart !== undefined && decimalPart.length > column.maxDecimals) {
          issues.push({
            field: column.field,
            severity: 'error',
            text: `${column.name} — max ${column.maxDecimals} decimal place${column.maxDecimals === 1 ? '' : 's'}.`,
          });
          continue;
        }
      }
      const { reject, warn } = this.limitsFor(column.key);
      const result = this.validation.evaluate(column.key, value, this.referenceData.limits());
      const shown = withUnit(formatNumber(value, 'en-US', '1.0-3'), reject?.unit ?? '');
      if (result.severity === 'Reject' && reject) {
        issues.push({
          field: column.field,
          severity: 'error',
          text: `${column.name} ${shown} — must be ${formatRange(reject, true)}.`,
        });
      } else if (result.severity === 'Warn' && warn) {
        issues.push({
          field: column.field,
          severity: 'warn',
          text: `${column.name} ${shown} — outside the normal ${formatRange(warn, true)}. Saved and flagged.`,
        });
      }
    }
    return [...issues, ...row.serverIssues()];
  }

  /** Red or amber for one cell. A blank cell only turns red after Save was tried. */
  protected cellState(row: NewRow, field: Field | 'takenAt'): 'error' | 'warn' | null {
    const issues = this.issuesFor(row).filter((issue) => issue.field === field);
    const hasValue = field === 'takenAt' ? !!row.takenAt() : !!row.values()[field].trim();
    if (issues.some((i) => i.severity === 'error') && (row.attempted() || hasValue)) return 'error';
    if (issues.some((i) => i.severity === 'warn')) return 'warn';
    return null;
  }

  protected cellTitle(row: NewRow, field: Field | 'takenAt'): string {
    return this.issuesFor(row)
      .filter((issue) => issue.field === field)
      .map((issue) => issue.text)
      .join(' ');
  }

  /** Enter (or the row's Save button): checks the row, then saves it on its own. */
  protected save(row: NewRow): void {
    const specimen = this.specimen();
    if (!specimen || row.locked()) return;
    row.attempted.set(true);
    if (this.issuesFor(row).some((issue) => issue.severity === 'error')) {
      row.status.set('failed');
      return;
    }

    row.status.set('saving');
    const values = row.values();
    const request: CreateCuringReadingRequest = {
      takenAt: new Date(row.takenAt()).toISOString(),
      internalTempF: parseNumber(values.internalTempF) as number,
      ambientTempF: parseNumber(values.ambientTempF) as number,
      moisturePct: parseNumber(values.moisturePct) as number,
      relativeHumidityPct: parseNumber(values.relativeHumidityPct) as number,
      resistivityOhmPerFt: parseNumber(values.resistivityOhmPerFt) as number,
      co2Ppm: parseNumber(values.co2Ppm) as number,
    };
    this.api.createCuringReading(specimen.id, request).subscribe({
      next: ({ warnings }) => {
        row.status.set(warnings.length > 0 ? 'flagged' : 'saved');
        // Ready for the next reading: a fresh row at the bottom, cursor in its first cell.
        if (this.rows().at(-1) === row) this.addRow();
      },
      error: (error: unknown) => {
        row.serverIssues.set(serverIssues(error));
        row.status.set('failed');
      },
    });
  }

  /** Saves every row not saved yet. Each one succeeds or fails on its own. */
  protected saveAll(): void {
    for (const row of this.rows()) {
      if (!row.locked()) this.save(row);
    }
  }

  private limitsFor(key: string): { reject?: ValidationLimit; warn?: ValidationLimit } {
    const limits = this.referenceData.limits().filter((limit) => limit.key === key);
    return {
      reject: limits.find((limit) => limit.severity === 'Reject'),
      warn: limits.find((limit) => limit.severity === 'Warn'),
    };
  }

  protected readingValue(reading: CuringReading, field: Field): number {
    return reading[field];
  }

  /** A saved row's value as a number, so it reads like the saved readings above it. */
  protected savedValue(row: NewRow, field: Field): number | null {
    return parseNumber(row.values()[field]);
  }
}

function cellId(row: NewRow, field: Field | 'takenAt'): string {
  return `reading-${row.id}-${field}`;
}

/** "720 °F", "84.2%": percent sits right against the number. */
function withUnit(value: string, unit: string): string {
  if (!unit || unit === 'ratio') return value;
  return unit === '%' ? `${value}%` : `${value} ${unit}`;
}

/** "0–150 °F", "≥ 0.01 ohms/ft". */
function formatRange(limit: ValidationLimit, unit: boolean): string {
  const n = (value: number) => formatNumber(value, 'en-US', '1.0-3');
  const text =
    limit.min !== null && limit.max !== null
      ? `${n(limit.min)}–${n(limit.max)}`
      : limit.min !== null
        ? `≥ ${n(limit.min)}`
        : `≤ ${n(limit.max as number)}`;
  return unit ? withUnit(text, limit.unit) : text;
}

/** A 400 from the server, as issues on the row's cells. Anything else becomes one row-level message. */
function serverIssues(error: unknown): Issue[] {
  const problem = describeApiError(error);
  if (problem.kind === 'validation') {
    return problem.fieldErrors.map(({ key, message }) => {
      const column = COLUMNS.find((c) => c.key === key);
      return {
        field: column?.field ?? 'takenAt',
        severity: 'error' as const,
        text: `${column?.name ?? 'Time taken'} — ${message}`,
      };
    });
  }
  const text =
    error instanceof HttpErrorResponse && error.status === 404
      ? "This cylinder wasn't found. Reload the page."
      : problem.message;
  return [{ field: 'takenAt', severity: 'error', text }];
}
