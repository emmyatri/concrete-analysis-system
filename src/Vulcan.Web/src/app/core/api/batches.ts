/**
 * Request and response shapes for saving a batch (M1-43).
 * Proposed to the API contract (M1-30): camelCase JSON, enums sent as strings.
 */

export type CementType = 'TypeI' | 'TypeII' | 'TypeIII';
export type ScmType = 'None' | 'FlyAsh' | 'Slag' | 'SilicaFume';

/** POST /api/batches: the batch, its composition and its cement oxides in one request. */
export interface CreateBatchRequest {
  batch: {
    batchNumber: string;
    castAt: string;
    finalWeightLb: number;
    expectedFinalBreakPsi: number;
    cementType: CementType;
    mixWaterPh: number;
    cementSurfacePh: number;
  };
  composition: {
    cementPct: number;
    waterPct: number;
    coarseAggregatePct: number;
    fineAggregatePct: number;
    scmType: ScmType;
    scmPct: number;
  };
  oxides: {
    aluminaPct: number;
    ironOxidePct: number;
    magnesiumOxidePct: number;
    sulfurTrioxidePct: number;
  };
}

/**
 * PUT /api/batches/{batchNumber}: the batch's fields at the top level, with its composition and
 * oxides. The batch number and IsSynthetic can't change.
 */
export interface UpdateBatchRequest {
  castAt: string;
  finalWeightLb: number;
  expectedFinalBreakPsi: number;
  cementType: CementType;
  mixWaterPh: number;
  cementSurfacePh: number;
  isSynthetic: boolean;
  composition: CreateBatchRequest['composition'];
  oxides: CreateBatchRequest['oxides'];
}

/** 201 response. */
export interface CreateBatchResponse {
  id: number;
  batchNumber: string;
  /** Warnings saved with the record, keyed like ValidationLimit.key. */
  warnings: { key: string; message: string }[];
}

/**
 * 400 response (ASP.NET ValidationProblemDetails).
 * `errors` keys match ValidationLimit.key, e.g. "Composition.CementPct" or "Batch.BatchNumber",
 * so each message can be shown next to its field.
 */
export interface ValidationProblem {
  title: string;
  status: number;
  errors: Record<string, string[]>;
}
