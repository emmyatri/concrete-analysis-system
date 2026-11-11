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
  /** Page number, starting at 1. Defaults to 1. */
  page?: number;
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
  entityType:
    'Batch' | 'Composition' | 'CementOxideAnalysis' | 'Specimen' | 'BreakResult' | 'CuringReading';
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
  /** Null on batches saved before it was added (client request, M1 demo). */
  cementSurfacePh: number | null;
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

/** POST /api/batches/{batchNumber}/specimens: one new cylinder (M1-61). */
export interface CreateSpecimenRequest {
  label: string;
  lengthIn: number;
  widthIn: number;
  testAgeDays: TestAge;
}

/** 201 response. */
export interface CreateSpecimenResponse {
  id: number;
  label: string;
}

/** GET /api/breaks/pending: a cylinder that hasn't been broken yet (M1-62, Break results). */
export interface PendingBreak {
  specimenId: number;
  batchNumber: string;
  label: string;
  testAgeDays: TestAge;
  /** The batch's cast date, ISO date-time, UTC. */
  castAt: string;
  isSynthetic: boolean;
}

/** POST /api/specimens/{specimenId}/break: the strength from crushing one cylinder (M1-62). */
export interface CreateBreakResultRequest {
  /** ISO date-time, UTC. Not before the batch's cast date and not in the future. */
  brokenAt: string;
  /** Whole PSI. */
  strengthPsi: number;
}

/** 201 response. A strength below 3,000 PSI saves and comes back as a warning. */
export interface CreateBreakResultResponse {
  id: number;
  warnings: RecordWarning[];
}

/** POST /api/specimens/{specimenId}/readings: one curing reading for one cylinder (M1-63). */
export interface CreateCuringReadingRequest {
  /** ISO date-time, UTC. Not before the batch's cast date and not in the future. */
  takenAt: string;
  internalTempF: number;
  ambientTempF: number;
  moisturePct: number;
  relativeHumidityPct: number;
  resistivityOhmPerFt: number;
  co2Ppm: number;
}

/** 201 response. Ambient outside 60–80 °F saves and comes back as a warning. */
export interface CreateCuringReadingResponse {
  id: number;
  warnings: RecordWarning[];
}

/** Whether there was enough curing data to estimate (ADR-005 section 5.1). */
export type StrengthStatus = 'Estimated' | 'InsufficientCuringData';

/** Which recorded breaks the curve was re-fit through (ADR-005 section 4.4). */
export type AdjustmentStage = 'None' | 'ThreeDay' | 'ThreeAndSevenDay';

/** One point on the strength chart: whole days after casting, whole PSI. */
export interface StrengthCurvePoint {
  day: number;
  psi: number;
}

/** The 3-, 7- and 28-day estimates. */
export interface StrengthEstimate {
  horizonDays: 3 | 7 | 28;
  estimatedPsi: number | null;
  equivalentAgeDays: number;
  adjustmentStage: AdjustmentStage;
  /** The estimate before the re-fit. Equal to estimatedPsi when adjustmentStage is None. */
  unadjustedPsi: number;
  basis: string;
  calibratedVersion: string;
  isSynthetic: boolean;
  /** Audit fields (FR-22), ISO date-times in UTC. */
  createdAtUtc: string;
  createdBy: string;
  modifiedAtUtc?: string;
  modifiedBy?: string;
}

/** A recorded break, placed on the chart at its actual age. */
export interface MeasuredBreak {
  specimenId: number;
  specimenLabel: string;
  ageDays: number;
  psi: number;
  isSynthetic: boolean;
}

/** An early warning (SCRUM-180). limitKey matches ValidationLimit.key; limit is the bound it crossed. */
export interface EarlyWarning {
  warning: string;
  field: string;
  value: number;
  limitKey: string;
  limit: number;
}

/** One curing reading with the cylinder's maturity so far, for the curing overlay. */
export interface CuringPoint {
  specimenId: number;
  specimenLabel: string;
  /** ISO date-time, UTC. */
  time: string;
  internalTempF: number;
  ambientTempF: number;
  maturityFHours: number;
  moisturePct: number;
  relativeHumidityPct: number;
  resistivityOhmPerFt: number;
  co2Ppm: number;
}

/** GET /api/batches/{batchNumber}/strength: the strength chart, estimates and early warnings (M2-30). */
export interface BatchStrength {
  id: number;
  batchNumber: string;
  isSynthetic: boolean;
  expectedFinalBreakPsi: number;
  cementType: BatchSummary['cementType'];
  status: StrengthStatus;
  atRisk: boolean;
  /** Null when status is InsufficientCuringData. */
  projected28DayPsi: number | null;
  /** ExpectedFinalBreakPsi minus projected28DayPsi. Negative means above target. */
  shortfallPsi: number | null;
  shortfallPct: number | null;
  /** Allowed % difference between the 28-day estimate and the actual 28-day break. Null when no percentage assigned. */
  accuracyTargetPct: number | null;
  /** Days 1–28. Empty when status is InsufficientCuringData. */
  curvePoints: StrengthCurvePoint[];
  /** The curve before the re-fit. Empty when no estimate was adjusted. */
  unadjustedCurvePoints: StrengthCurvePoint[];
  estimates: StrengthEstimate[];
  measuredBreaks: MeasuredBreak[];
  earlyWarnings: EarlyWarning[];
  curingPoints: CuringPoint[];
}

/** GET /api/warnings/early: one batch with at least one early warning (Home, batches at risk). */
export interface EarlyWarningBatch {
  id: number;
  batchNumber: string;
  isSynthetic: boolean;
  atRisk: boolean;
  projected28DayPsi: number | null;
  shortfallPsi: number | null;
  shortfallPct: number | null;
  earlyWarnings: EarlyWarning[];
}
