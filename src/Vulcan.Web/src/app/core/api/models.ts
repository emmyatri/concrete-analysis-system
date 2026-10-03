/**
 * Shapes of the data the API sends and receives (M1-60). JSON is camelCase; enums are strings.
 * Proposed to the API contract (M1-30).
 */

export type ValidationSeverity = 'Reject' | 'Warn';

/** GET /api/validation-limits: one row per key and severity (schema.md section 4). */
export interface ValidationLimit {
  key: string;
  severity: ValidationSeverity;
  min: number | null;
  max: number | null;
  unit: string;
  message: string;
  source: string;
}

export type Material = 'Cement' | 'Water' | 'FlyAsh' | 'Slag' | 'SilicaFume';

/** GET /api/specific-gravities */
export interface SpecificGravity {
  material: Material;
  value: number;
}

/** A value that saved but was flagged (RecordWarning), keyed like ValidationLimit.key. */
export interface RecordWarning {
  key: string;
  message: string;
}

/** One row in Recent batches (Home) and Search batches. */
export interface BatchSummary {
  id: number;
  batchNumber: string;
  /** ISO date-time, UTC. */
  castAt: string;
  cementType: 'TypeI' | 'TypeII' | 'TypeIII';
  expectedFinalBreakPsi: number;
  waterCementRatio: number | null;
  cylinderCount: number;
  breaksRecorded: number;
  /** Null until a 28-day break is recorded. */
  average28DayPsi: number | null;
  warningCount: number;
  isSynthetic: boolean;
}

/** GET /api/batches query. Every field is optional. */
export interface BatchQuery {
  /** Any part of the batch number, upper or lower case. */
  search?: string;
  /** ISO dates, inclusive. */
  castFrom?: string;
  castTo?: string;
  cementType?: BatchSummary['cementType'];
  hasWarnings?: boolean;
  /** Defaults to true. */
  includeSynthetic?: boolean;
}
