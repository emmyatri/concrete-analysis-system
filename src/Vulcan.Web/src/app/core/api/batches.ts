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
