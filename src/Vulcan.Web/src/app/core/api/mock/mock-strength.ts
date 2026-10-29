import {
  AdjustmentStage,
  BatchDetail,
  BatchStrength,
  CuringPoint,
  EarlyWarning,
  EarlyWarningBatch,
  MeasuredBreak,
  Specimen,
  StrengthCurvePoint,
  StrengthEstimate,
} from '../models';
import { MOCK_VALIDATION_LIMITS } from './mock-data';

/**
 * Fake strength predictions (GET /api/batches/{batchNumber}/strength, GET /api/warnings/early),
 * development builds only. They're worked out from each batch's own readings and breaks with the
 * ADR-005 method, so a reading or break saved in mock mode moves the chart like it will on the server.
 * The real estimator is Vulcan.Prediction; if the two disagree, the server is right.
 */

/** ADR-005 section 3: Nurse–Saul datum, −10 °C per the NDOT maturity method (SCRUM-194), and 23 °C reference. */
const DATUM_TEMP_F = 14;
const REFERENCE_TEMP_F = 73.4;

/** ADR-005 section 4.2: ACI 209R-92 coefficients by cement type. */
const COEFFICIENTS = {
  TypeI: { a: 4.0, b: 0.85 },
  TypeII: { a: 4.0, b: 0.85 },
  TypeIII: { a: 2.3, b: 0.92 },
} as const;

const CALIBRATION_VERSION = 'v1';

/** Early warning limits (client-decisions section 13). They move to the ValidationLimit seed in SCRUM-181. */
const MINIMUM_PSI = 3000;
const SHORTFALL_PCT = 5;
const EXPECTED_FINAL_VS_WCM_PCT = 30;

/** ADR-005 section 4.5: ACI 211.1-97 Table 6.3.4(a), non-air-entrained: [w/cm, 28-day PSI]. */
const ACI_WCM_TABLE: [number, number][] = [
  [0.33, 7000],
  [0.41, 6000],
  [0.48, 5000],
  [0.57, 4000],
  [0.68, 3000],
  [0.82, 2000],
];

const HOUR_MS = 60 * 60 * 1000;
const DAY_MS = 24 * HOUR_MS;
const HORIZONS = [3, 7, 28] as const;

interface Reading {
  time: number;
  tempF: number;
}

/**
 * ADR-005 sections 3 and 5.2: °F·h from casting to the target for one cylinder. Before the first
 * reading its temperature applies; between readings the average of the two; past the last reading,
 * the temperature is interpolated toward the next reading, or the average of the readings so far.
 */
export function maturityFHours(castAt: number, readings: Reading[], target: number): number {
  const valid = readings.filter((r) => r.time >= castAt).sort((x, y) => x.time - y.time);
  if (valid.length === 0 || target <= castAt) return 0;

  const intervals: { start: number; end: number; tempF: number }[] = [];
  const first = valid[0];
  if (target <= first.time) {
    intervals.push({ start: castAt, end: target, tempF: first.tempF });
  } else {
    const applicable = valid.filter((r) => r.time <= target);
    if (first.time > castAt) intervals.push({ start: castAt, end: first.time, tempF: first.tempF });
    for (let i = 1; i < applicable.length; i++) {
      const [previous, current] = [applicable[i - 1], applicable[i]];
      intervals.push({
        start: previous.time,
        end: current.time,
        tempF: (previous.tempF + current.tempF) / 2,
      });
    }
    const last = applicable[applicable.length - 1];
    if (last.time < target) {
      const next = valid.find((r) => r.time > target);
      const targetTempF = next
        ? last.tempF + ((next.tempF - last.tempF) * (target - last.time)) / (next.time - last.time)
        : undefined;
      intervals.push({
        start: last.time,
        end: target,
        tempF:
          targetTempF === undefined
            ? average(applicable.map((r) => r.tempF))
            : (last.tempF + targetTempF) / 2,
      });
    }
  }

  return intervals.reduce(
    (sum, { start, end, tempF }) =>
      sum + (Math.max(0, tempF - DATUM_TEMP_F) * (end - start)) / HOUR_MS,
    0,
  );
}

/** ADR-005 Equation 3: days at the reference temperature that give the same maturity. */
function equivalentAgeDays(maturity: number): number {
  return maturity / ((REFERENCE_TEMP_F - DATUM_TEMP_F) * 24);
}

/** ADR-005 Equation 4 (ACI 209R-92). */
function aciPsi(ageDays: number, a: number, b: number, referencePsi: number): number {
  return (ageDays / (a + b * ageDays)) * referencePsi;
}

/**
 * ADR-005 section 4.4: re-fits a (and, with 7-day breaks too, the reference strength) so the curve
 * passes through the average recorded breaks. Skipped when the 3-day break is more than 30% off the
 * 3-day estimate, and falls back a stage whenever a re-fit value isn't a positive number.
 */
function adjustment(
  coefficients: { a: number; b: number },
  referencePsi: number,
  t3: number,
  t7: number,
  break3: number | null,
  break7: number | null,
): { stage: AdjustmentStage; a: number; referencePsi: number } {
  const { a, b } = coefficients;
  const none = { stage: 'None' as const, a, referencePsi };
  if (break3 === null || break3 <= 0) return none;
  const estimate3 = aciPsi(t3, a, b, referencePsi);
  if (Math.abs(break3 - estimate3) / estimate3 > 0.3) return none;

  const positive = (value: number) => Number.isFinite(value) && value > 0;
  const aThree = t3 * (referencePsi / break3 - b);
  const threeDay = positive(aThree)
    ? { stage: 'ThreeDay' as const, a: aThree, referencePsi }
    : none;
  if (break7 === null || break7 <= 0) return threeDay;

  const k = (1 / break3 - 1 / break7) / (1 / t3 - 1 / t7);
  const c = 1 / break3 - k / t3;
  const fitted = b / c;
  const aSeven = k * fitted;
  return positive(fitted) && positive(aSeven)
    ? { stage: 'ThreeAndSevenDay', a: aSeven, referencePsi: fitted }
    : threeDay;
}

/** The fake GET /api/batches/{batchNumber}/strength response for one batch. */
export function mockStrength(batch: BatchDetail): BatchStrength {
  const castAt = Date.parse(batch.castAt);
  const readingsOf = (specimen: Specimen): Reading[] =>
    specimen.curingReadings.map((r) => ({ time: Date.parse(r.takenAt), tempF: r.internalTempF }));

  const curingPoints: CuringPoint[] = batch.specimens.flatMap((specimen) =>
    specimen.curingReadings.map((r) => ({
      specimenId: specimen.id,
      specimenLabel: specimen.label,
      time: r.takenAt,
      internalTempF: r.internalTempF,
      ambientTempF: r.ambientTempF,
      maturityFHours: round(maturityFHours(castAt, readingsOf(specimen), Date.parse(r.takenAt)), 1),
      moisturePct: r.moisturePct,
      relativeHumidityPct: r.relativeHumidityPct,
      resistivityOhmPerFt: r.resistivityOhmPerFt,
      co2Ppm: r.co2Ppm,
    })),
  );

  const measuredBreaks: MeasuredBreak[] = batch.specimens
    .filter((specimen) => specimen.breakResult)
    .map((specimen) => ({
      specimenId: specimen.id,
      specimenLabel: specimen.label,
      ageDays: round((Date.parse(specimen.breakResult!.brokenAt) - castAt) / DAY_MS, 2),
      psi: specimen.breakResult!.strengthPsi,
      isSynthetic: specimen.isSynthetic,
    }));

  const result: BatchStrength = {
    id: batch.id,
    batchNumber: batch.batchNumber,
    isSynthetic: batch.isSynthetic,
    expectedFinalBreakPsi: batch.expectedFinalBreakPsi,
    cementType: batch.cementType,
    status: 'InsufficientCuringData',
    atRisk: false,
    projected28DayPsi: null,
    shortfallPsi: null,
    shortfallPct: null,
    curvePoints: [],
    unadjustedCurvePoints: [],
    estimates: [],
    measuredBreaks,
    earlyWarnings: [...rangeWarnings(batch), ...waterCementWarnings(batch)],
    curingPoints,
  };

  // ADR-005 section 5.1: 2+ readings from casting on, the latest at least 24 hours after casting.
  const qualifying = batch.specimens
    .map(readingsOf)
    .map((readings) => readings.filter((r) => r.time >= castAt))
    .filter(
      (readings) =>
        readings.length >= 2 && Math.max(...readings.map((r) => r.time)) >= castAt + DAY_MS,
    );
  if (qualifying.length === 0) return result;

  // ADR-005 section 5.3: average the cylinders' equivalent ages for each day, then apply Equation 4 once.
  const ages = Array.from({ length: 28 }, (_, i) =>
    average(
      qualifying.map((readings) =>
        equivalentAgeDays(maturityFHours(castAt, readings, castAt + (i + 1) * DAY_MS)),
      ),
    ),
  );
  const coefficients = COEFFICIENTS[batch.cementType];
  const reference = batch.expectedFinalBreakPsi;
  const unadjusted = ages.map((age) => aciPsi(age, coefficients.a, coefficients.b, reference));

  const fit = adjustment(
    coefficients,
    reference,
    ages[2],
    ages[6],
    averageBreak(batch, 'Day3'),
    averageBreak(batch, 'Day7'),
  );
  const adjusted = ages.map((age) => aciPsi(age, fit.a, coefficients.b, fit.referencePsi));

  const basis = basisText(batch, qualifying, castAt, fit);
  const estimates: StrengthEstimate[] = HORIZONS.map((day) => ({
    horizonDays: day,
    estimatedPsi: Math.round(adjusted[day - 1]),
    equivalentAgeDays: round(ages[day - 1], 2),
    adjustmentStage: fit.stage,
    unadjustedPsi: Math.round(unadjusted[day - 1]),
    basis,
    calibratedVersion: CALIBRATION_VERSION,
    isSynthetic: batch.isSynthetic,
    createdAtUtc: batch.createdAtUtc,
    createdBy: 'System',
  }));

  const projected = Math.round(adjusted[27]);
  const shortfallPsi = reference - projected;
  const shortfallPct = round((shortfallPsi / reference) * 100, 2);
  const predictionWarnings: EarlyWarning[] = [];
  if (projected < MINIMUM_PSI) {
    predictionWarnings.push({
      warning: `Projected 28-day strength of ${psi(projected)} is below the ${psi(MINIMUM_PSI)} minimum`,
      field: 'projected28DayPsi',
      value: projected,
      limitKey: 'Prediction.MinimumPsi',
      limit: MINIMUM_PSI,
    });
  }
  if (shortfallPct > SHORTFALL_PCT) {
    predictionWarnings.push({
      warning: `Projected 28-day strength of ${psi(projected)} is ${shortfallPct}% below the expected ${psi(reference)}`,
      field: 'shortfallPct',
      value: shortfallPct,
      limitKey: 'Prediction.ShortfallPct',
      limit: SHORTFALL_PCT,
    });
  }

  return {
    ...result,
    status: 'Estimated',
    atRisk: predictionWarnings.length > 0,
    projected28DayPsi: projected,
    shortfallPsi,
    shortfallPct,
    curvePoints: adjusted.map((value, i) => ({ day: i + 1, psi: Math.round(value) })),
    unadjustedCurvePoints:
      fit.stage === 'None'
        ? []
        : unadjusted.map((value, i): StrengthCurvePoint => ({
            day: i + 1,
            psi: Math.round(value),
          })),
    estimates,
    earlyWarnings: [...predictionWarnings, ...result.earlyWarnings],
  };
}

/** The fake GET /api/warnings/early response: batches with any early warning, at-risk ones first. */
export function mockEarlyWarnings(
  batches: BatchDetail[],
  includeSynthetic: boolean,
): EarlyWarningBatch[] {
  return batches
    .filter((batch) => includeSynthetic || !batch.isSynthetic)
    .map(mockStrength)
    .filter((strength) => strength.earlyWarnings.length > 0)
    .sort(
      (a, b) =>
        Number(b.atRisk) - Number(a.atRisk) ||
        (b.shortfallPct ?? -Infinity) - (a.shortfallPct ?? -Infinity) ||
        a.batchNumber.localeCompare(b.batchNumber),
    )
    .map((strength) => ({
      id: strength.id,
      batchNumber: strength.batchNumber,
      isSynthetic: strength.isSynthetic,
      atRisk: strength.atRisk,
      projected28DayPsi: strength.projected28DayPsi,
      shortfallPsi: strength.shortfallPsi,
      shortfallPct: strength.shortfallPct,
      earlyWarnings: strength.earlyWarnings,
    }));
}

/**
 * Tier 1 (client-decisions section 13): the batch-entry and curing values already saved with a
 * Warn-limit warning. Break results aren't early warnings, so they're left out. Each factor is listed
 * once: a cold batch has an ambient warning on every reading, but that's one reason.
 */
function rangeWarnings(batch: BatchDetail): EarlyWarning[] {
  return batch.warnings
    .filter((saved) => saved.entityType !== 'BreakResult')
    .filter(
      (saved, index, all) => all.findIndex((other) => other.limitKey === saved.limitKey) === index,
    )
    .map((saved) => {
      const limit = MOCK_VALIDATION_LIMITS.find(
        (row) => row.key === saved.limitKey && row.severity === 'Warn',
      );
      const bound = limit?.max != null && saved.value > limit.max ? limit.max : limit?.min;
      return {
        warning: saved.message,
        field: saved.fieldName[0].toLowerCase() + saved.fieldName.slice(1),
        value: saved.value,
        limitKey: saved.limitKey,
        limit: bound ?? saved.value,
      };
    });
}

/** ADR-005 section 4.5: is the expected final reasonable for this much water? */
function waterCementWarnings(batch: BatchDetail): EarlyWarning[] {
  const ratio = batch.composition.waterCementRatio;
  if (ratio === null || ratio < ACI_WCM_TABLE[0][0]) return [];
  const tablePsi = aciTablePsi(ratio);
  const pct = round(((batch.expectedFinalBreakPsi - tablePsi) / tablePsi) * 100, 1);
  if (pct <= EXPECTED_FINAL_VS_WCM_PCT) return [];
  return [
    {
      warning: `Expected final of ${psi(batch.expectedFinalBreakPsi)} is ${pct}% above the ${psi(Math.round(tablePsi))} typical for a w/cm of ${ratio}`,
      field: 'expectedFinalVsWcmPct',
      value: pct,
      limitKey: 'Batch.ExpectedFinalVsWcmPct',
      limit: EXPECTED_FINAL_VS_WCM_PCT,
    },
  ];
}

/** Linear interpolation in the ACI table. Above 0.82 the 2,000 PSI value is used. */
function aciTablePsi(ratio: number): number {
  const last = ACI_WCM_TABLE[ACI_WCM_TABLE.length - 1];
  if (ratio >= last[0]) return last[1];
  const upper = ACI_WCM_TABLE.findIndex(([wcm]) => wcm >= ratio);
  if (upper <= 0) return ACI_WCM_TABLE[0][1];
  const [[w0, p0], [w1, p1]] = [ACI_WCM_TABLE[upper - 1], ACI_WCM_TABLE[upper]];
  return p0 + ((p1 - p0) * (ratio - w0)) / (w1 - w0);
}

/** The average break for one test age, or null when none are recorded yet. */
function averageBreak(batch: BatchDetail, age: Specimen['testAgeDays']): number | null {
  const breaks = batch.specimens
    .filter((specimen) => specimen.testAgeDays === age && specimen.breakResult)
    .map((specimen) => specimen.breakResult!.strengthPsi);
  return breaks.length > 0 ? average(breaks) : null;
}

/** The explanation shown with each estimate, worded like the server's StrengthEstimator. */
function basisText(
  batch: BatchDetail,
  qualifying: Reading[][],
  castAt: number,
  fit: { stage: AdjustmentStage; referencePsi: number },
): string {
  const lastHours = qualifying.map(
    (readings) => (Math.max(...readings.map((r) => r.time)) - castAt) / HOUR_MS,
  );
  const [earliest, latest] = [Math.min(...lastHours), Math.max(...lastHours)];
  const hours = (value: number) => String(round(value, 1));
  const coverage =
    earliest === latest ? `${hours(latest)} hours` : `${hours(earliest)}–${hours(latest)} hours`;
  const count = qualifying.length;
  let text =
    `ACI 209R-92 (${batch.cementType}) using ${count} qualifying ${count === 1 ? 'specimen' : 'specimens'}, ` +
    `with last curing readings at ${coverage} after casting ` +
    `and reference strength ${batch.expectedFinalBreakPsi} PSI. ` +
    'Equivalent curing ages were averaged across qualifying specimens.';
  if (earliest < 28 * 24) {
    text +=
      " Beyond each specimen's last recorded temperature, " +
      'the average of its available curing temperatures was assumed.';
  }
  if (fit.stage === 'ThreeDay') text += ' Re-fit through the average 3-day break.';
  if (fit.stage === 'ThreeAndSevenDay') {
    text += ` Re-fit through the average 3- and 7-day breaks (reference strength ${Math.round(fit.referencePsi)} PSI).`;
  }
  return text;
}

function average(values: number[]): number {
  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

function round(value: number, places: number): number {
  return Math.round(value * 10 ** places) / 10 ** places;
}

function psi(value: number): string {
  return `${value.toLocaleString('en-US')} PSI`;
}
