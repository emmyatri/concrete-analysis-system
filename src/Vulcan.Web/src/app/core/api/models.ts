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


export type ScmType = 'None' | 'FlyAsh' | 'Slag' | 'SilicaFume';
export type TestAge = 'Day3' | 'Day7' | 'Day28';

/** The mix recipe, % by absolute volume. The last three are calculated by the server, never stored. */
export interface BatchComposition {
  cementPct: number;
  waterPct: number;
  coarseAggregatePct: number;
  fineAggregatePct: number;
  scmType: ScmType;
  scmPct: number;
  totalPct: number;
  airOtherPct: number;
  /** Null when there's no cement or SCM. */
  waterCementRatio: number | null;
}

/** Cement chemistry from the mill certificate, % of the cement. */
export interface BatchOxides {
  aluminaPct: number;
  ironOxidePct: number;
  magnesiumOxidePct: number;
  sulfurTrioxidePct: number;
}

export interface CuringReading {
  id: number;
  /** ISO date-time, UTC. */
  takenAt: string;
  internalTempF: number;
  ambientTempF: number;
  moisturePct: number;
  relativeHumidityPct: number;
  resistivityOhmPerFt: number;
  co2Ppm: number;
}

export interface BreakResult {
  id: number;
  /** ISO date-time, UTC. */
  brokenAt: string;
  strengthPsi: number;
}

/** One test cylinder. */
export interface Specimen {
  id: number;
  label: string;
  lengthIn: number;
  widthIn: number;
  testAgeDays: TestAge;
  isSynthetic: boolean;
  /** Null until it's broken. */
  breakResult: BreakResult | null;
  /** Oldest first. */
  curingReadings: CuringReading[];
}

/** A value that saved but was flagged (RecordWarning table). entityType + entityId say which record. */
export interface SavedWarning {
  id: number;
  entityType: 'Batch' | 'Composition' | 'CementOxideAnalysis' | 'Specimen' | 'BreakResult' | 'CuringReading';
  entityId: number;
  fieldName: string;
  /** Matches ValidationLimit.key, e.g. "Break.StrengthPsi". */
  limitKey: string;
  value: number;
  message: string;
  /** ISO date-time, UTC. */
  createdAtUtc: string;
}

/** GET /api/batches/{batchNumber}: everything about one batch (Batch Detail). */
export interface BatchDetail {
  id: number;
  batchNumber: string;
  /** ISO date-time, UTC. */
  castAt: string;
  finalWeightLb: number;
  expectedFinalBreakPsi: number;
  cementType: BatchSummary['cementType'];
  mixWaterPh: number;
  isSynthetic: boolean;
  /** Audit fields (FR-22), ISO date-times in UTC. */
  createdAtUtc: string;
  createdBy: string;
  modifiedAtUtc: string | null;
  modifiedBy: string | null;
  composition: BatchComposition;
  oxides: BatchOxides;
  specimens: Specimen[];
  /** Every saved warning for this batch, its composition, oxides, cylinders, breaks and readings. */
  warnings: SavedWarning[];
}
