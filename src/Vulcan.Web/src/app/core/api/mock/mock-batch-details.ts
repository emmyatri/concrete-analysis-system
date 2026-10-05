import synthetic from '../../../../../../../data/synthetic/batches.json';
import { CreateBatchRequest } from '../batches';
import { BatchComposition, BatchDetail, SavedWarning, ScmType, Specimen, TestAge } from '../models';
import { MOCK_BATCHES, MOCK_SPECIFIC_GRAVITIES, MOCK_VALIDATION_LIMITS } from './mock-data';

/**
 * Full batches for Batch Detail (GET /api/batches/{batchNumber}), development builds only.
 * The VM-SYN batches are read straight from data/synthetic/batches.json, the same file the server seeds,
 * so the two can't drift apart. Warnings are worked out from the Warn limits, the way the server saves them.
 */

const SEEDED_BY = 'Seed data';

function calculate(
  composition: Omit<BatchComposition, 'totalPct' | 'airOtherPct' | 'waterCementRatio'>,
) {
  const gravity = (material: string) =>
    MOCK_SPECIFIC_GRAVITIES.find((row) => row.material === material)?.value ?? 0;
  const totalPct =
    composition.cementPct +
    composition.waterPct +
    composition.coarseAggregatePct +
    composition.fineAggregatePct +
    composition.scmPct;
  const cementitious =
    composition.cementPct * gravity('Cement') +
    (composition.scmType === 'None' ? 0 : composition.scmPct * gravity(composition.scmType));
  return {
    ...composition,
    totalPct: round(totalPct, 2),
    airOtherPct: round(100 - totalPct, 2),
    waterCementRatio:
      cementitious > 0 ? round((composition.waterPct * gravity('Water')) / cementitious, 3) : null,
  };
}

function round(value: number, places: number) {
  return Math.round(value * 10 ** places) / 10 ** places;
}

/** Adds a warning for every Warn limit a value is outside of. */
function warningsFor(batch: Omit<BatchDetail, 'warnings'>): SavedWarning[] {
  const warnings: SavedWarning[] = [];
  const check = (
    entityType: SavedWarning['entityType'],
    entityId: number,
    limitKey: string,
    value: number | null,
  ) => {
    if (value === null) return;
    const limit = MOCK_VALIDATION_LIMITS.find(
      (row) => row.key === limitKey && row.severity === 'Warn',
    );
    if (!limit) return;
    const outside =
      (limit.min !== null && value < limit.min) || (limit.max !== null && value > limit.max);
    if (!outside) return;
    warnings.push({
      id: batch.id * 1000 + warnings.length + 1,
      entityType,
      entityId,
      fieldName: limitKey.split('.')[1],
      limitKey,
      value,
      message: limit.message,
      createdAtUtc: batch.createdAtUtc,
    });
  };

  check('Batch', batch.id, 'Batch.MixWaterPh', batch.mixWaterPh);
  check(
    'Composition',
    batch.id,
    'Composition.WaterCementRatio',
    batch.composition.waterCementRatio,
  );
  for (const [field, value] of Object.entries(batch.oxides)) {
    check(
      'CementOxideAnalysis',
      batch.id,
      `Oxide.${field[0].toUpperCase()}${field.slice(1)}`,
      value,
    );
  }
  for (const specimen of batch.specimens) {
    if (specimen.breakResult) {
      check(
        'BreakResult',
        specimen.breakResult.id,
        'Break.StrengthPsi',
        specimen.breakResult.strengthPsi,
      );
    }
    for (const reading of specimen.curingReadings) {
      check('CuringReading', reading.id, 'Curing.AmbientTempF', reading.ambientTempF);
    }
  }
  return warnings;
}

function fromSynthetic(row: (typeof synthetic.Batches)[number]): BatchDetail {
  const id = MOCK_BATCHES.find((summary) => summary.batchNumber === row.BatchNumber)?.id ?? 0;
  let readingId = id * 10000;

  const specimens: Specimen[] = row.Specimens.map((specimen, index) => ({
    id: id * 100 + index + 1,
    label: specimen.Label,
    lengthIn: specimen.LengthIn,
    widthIn: specimen.WidthIn,
    testAgeDays: specimen.TestAgeDays as TestAge,
    isSynthetic: specimen.IsSynthetic,
    breakResult: specimen.BreakResult
      ? {
        id: id * 100 + index + 1,
        brokenAt: specimen.BreakResult.BrokenAt,
        strengthPsi: specimen.BreakResult.StrengthPsi,
      }
      : null,
    curingReadings: specimen.CuringReadings.map((reading) => ({
      id: ++readingId,
      takenAt: reading.TakenAt,
      internalTempF: reading.InternalTempF,
      ambientTempF: reading.AmbientTempF,
      moisturePct: reading.MoisturePct,
      relativeHumidityPct: reading.RelativeHumidityPct,
      resistivityOhmPerFt: reading.ResistivityOhmPerFt,
      co2Ppm: reading.Co2Ppm,
    })),
  }));

  const batch = {
    id,
    batchNumber: row.BatchNumber,
    castAt: row.CastAt,
    finalWeightLb: row.FinalWeightLb,
    expectedFinalBreakPsi: row.ExpectedFinalBreakPsi,
    cementType: row.CementType as BatchDetail['cementType'],
    mixWaterPh: row.MixWaterPh,
    isSynthetic: row.IsSynthetic,
    createdAtUtc: row.CastAt,
    createdBy: SEEDED_BY,
    modifiedAtUtc: null,
    modifiedBy: null,
    composition: calculate({
      cementPct: row.Composition.CementPct,
      waterPct: row.Composition.WaterPct,
      coarseAggregatePct: row.Composition.CoarseAggregatePct,
      fineAggregatePct: row.Composition.FineAggregatePct,
      scmType: row.Composition.ScmType as ScmType,
      scmPct: row.Composition.ScmPct,
    }),
    oxides: {
      aluminaPct: row.CementOxideAnalysis.AluminaPct,
      ironOxidePct: row.CementOxideAnalysis.IronOxidePct,
      magnesiumOxidePct: row.CementOxideAnalysis.MagnesiumOxidePct,
      sulfurTrioxidePct: row.CementOxideAnalysis.SulfurTrioxidePct,
    },
    specimens,
  };
  return { ...batch, warnings: warningsFor(batch) };
}

/** VM-2026-0142 from the hi-fi mockup: a real (not synthetic) batch, cylinders cast but none broken yet. */
function mockupBatch(): BatchDetail {
  const ages: TestAge[] = ['Day3', 'Day3', 'Day7', 'Day7', 'Day28', 'Day28'];
  const batch = {
    id: 142,
    batchNumber: 'VM-2026-0142',
    castAt: '2026-09-29T11:40:00Z',
    finalWeightLb: 4380,
    expectedFinalBreakPsi: 6000,
    cementType: 'TypeIII' as const,
    mixWaterPh: 7.2,
    isSynthetic: false,
    createdAtUtc: '2026-09-29T11:52:00Z',
    createdBy: 'J. Rivera',
    modifiedAtUtc: '2026-09-29T12:05:00Z',
    modifiedBy: 'J. Rivera',
    composition: calculate({
      cementPct: 14,
      waterPct: 15,
      coarseAggregatePct: 42,
      fineAggregatePct: 25,
      scmType: 'FlyAsh',
      scmPct: 2,
    }),
    oxides: { aluminaPct: 4.8, ironOxidePct: 2.8, magnesiumOxidePct: 1.8, sulfurTrioxidePct: 2.2 },
    specimens: ages.map((age, index) => ({
      id: 14201 + index,
      label: `A${index + 1}`,
      lengthIn: 12,
      widthIn: 6,
      testAgeDays: age,
      isSynthetic: false,
      breakResult: null,
      curingReadings: [],
    })),
  };
  return { ...batch, warnings: warningsFor(batch) };
}

export const MOCK_BATCH_DETAILS: BatchDetail[] = [
  mockupBatch(),
  ...synthetic.Batches.map(fromSynthetic),
];


/** A batch saved from New batch while the app is open, in the same shape as the fake ones. */
export function mockBatchDetailFromRequest(
  id: number,
  request: CreateBatchRequest,
  createdBy: string,
): BatchDetail {
  const batch = {
    id,
    batchNumber: request.batch.batchNumber,
    castAt: request.batch.castAt,
    finalWeightLb: request.batch.finalWeightLb,
    expectedFinalBreakPsi: request.batch.expectedFinalBreakPsi,
    cementType: request.batch.cementType,
    mixWaterPh: request.batch.mixWaterPh,
    isSynthetic: false,
    createdAtUtc: new Date().toISOString(),
    createdBy,
    modifiedAtUtc: null,
    modifiedBy: null,
    composition: calculate({ ...request.composition }),
    oxides: { ...request.oxides },
    specimens: [],
  };
  return { ...batch, warnings: warningsFor(batch) };
}
