// Synthetic M2 dataset generator (SCRUM-166).
//
// Every strength effect here is MADE UP FOR TESTING (SLA 1.3). None of it is real Vulcan data or
// a validated engineering relationship. See docs/synthetic-data.md for the effects and sizes.
//
// The curing readings from the M1 dataset are kept as they are, except for two demo cases:
//   VM-SYN-002 cold-cured: internal and ambient temperatures shifted down by COLD_SHIFT_F.
//   VM-SYN-003 moisture loss: moisture falls MOISTURE_DROP points between 24 h and 72 h.
// Both changes are applied only once, so running the generator again leaves the data unchanged.
// Break strengths are then recalculated for every specimen from its own curing readings.
//
//   node data/synthetic/generate.mjs           rewrite batches.json
//   node data/synthetic/generate.mjs --check   fail if batches.json differs from the generator
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { isDeepStrictEqual } from 'node:util';
import { pathToFileURL } from 'node:url';

// Chosen so that only the named demo cases are flagged with ±5% scatter (checked by flags()).
export const SEED = 168;
export const WATER_REFERENCE = 0.5; // w/cm above this lowers strength
export const WATER_SLOPE = 2.0; // 2% per 0.01 of w/cm above the reference
export const MOISTURE_SLOPE = 0.08; // 8% per point of batch-average moisture lost, day 1 to day 3
export const MOISTURE_NOISE = 1.5; // average losses below this many points are normal variation
export const SCM_DELAY = 0.1; // day-3 reduction = 10% of the SCM share of cementitious mass
export const COLD_SHIFT_F = 30; // cold-cured case: °F below the M1 temperatures
export const MOISTURE_DROP = 2; // moisture-loss case: points lost between 24 h and 72 h
export const COLD_CASE = 'VM-SYN-002';
export const MOISTURE_CASE = 'VM-SYN-003';

const SPECIFIC_GRAVITIES = { None: 0, FlyAsh: 2.35, Slag: 2.9, SilicaFume: 2.35 };
const CEMENT_SPECIFIC_GRAVITY = 3.15;
const HOUR_MS = 3_600_000;

const hoursAfterCast = (batch, reading) =>
  (Date.parse(reading.TakenAt) - Date.parse(batch.CastAt)) / HOUR_MS;
const round1 = value => Math.round(value * 10) / 10;

/** Deterministic per-specimen multiplier between 0.95 and 1.05. */
export function scatter(batchNumber, label) {
  const hash = createHash('sha256').update(`${SEED}:${batchNumber}:${label}`).digest();
  return 0.95 + 0.1 * (hash.readUInt32BE(0) / 2 ** 32);
}

/** ADR-005 Nurse-Saul equivalent age (days at 73.4 °F) at `day` days after cast. */
export function equivalentAge(castAt, readings, day, calibration) {
  const points = readings
    .map(reading => ({
      hours: (Date.parse(reading.TakenAt) - Date.parse(castAt)) / HOUR_MS,
      temperature: reading.InternalTempF,
    }))
    .sort((left, right) => left.hours - right.hours);
  const targetHours = day * 24;
  const applicable = points.filter(point => point.hours <= targetHours);
  const datum = calibration.DatumTemperatureF;
  let maturity = Math.max(0, points[0].temperature - datum) * Math.min(points[0].hours, targetHours);
  for (let index = 1; index < applicable.length; index++) {
    const previous = applicable[index - 1];
    const current = applicable[index];
    maturity +=
      Math.max(0, (previous.temperature + current.temperature) / 2 - datum) * (current.hours - previous.hours);
  }
  const last = applicable.at(-1);
  if (last && last.hours < targetHours) {
    const following = points.find(point => point.hours > targetHours);
    let average = applicable.reduce((sum, point) => sum + point.temperature, 0) / applicable.length;
    if (following) {
      const interpolated =
        last.temperature +
        ((following.temperature - last.temperature) * (targetHours - last.hours)) / (following.hours - last.hours);
      average = (last.temperature + interpolated) / 2;
    }
    maturity += Math.max(0, average - datum) * (targetHours - last.hours);
  }
  return maturity / ((calibration.ReferenceTemperatureF - datum) * 24);
}

/** Batch-average moisture at 24 h minus at 72 h, in percentage points (positive = moisture lost). */
export function moistureLoss(batch) {
  const averageAt = hours => {
    const values = batch.Specimens.flatMap(specimen => specimen.CuringReadings)
      .filter(reading => Math.abs(hoursAfterCast(batch, reading) - hours) < 0.01)
      .map(reading => reading.MoisturePct);
    return values.reduce((sum, value) => sum + value, 0) / values.length;
  };
  return averageAt(24) - averageAt(72);
}

/** Water-to-cementitious ratio by mass (water specific gravity 1.0). */
export function waterCementitiousRatio(composition) {
  const binder =
    composition.CementPct * CEMENT_SPECIFIC_GRAVITY + composition.ScmPct * SPECIFIC_GRAVITIES[composition.ScmType];
  return composition.WaterPct / binder;
}

export function calculateBreak(batch, specimen, calibration) {
  const composition = batch.Composition;
  const cementMass = composition.CementPct * CEMENT_SPECIFIC_GRAVITY;
  const scmMass = composition.ScmPct * SPECIFIC_GRAVITIES[composition.ScmType];
  const day = Number(specimen.TestAgeDays.slice(3));

  const lost = Math.max(0, moistureLoss(batch));
  const effectiveLoss = lost >= MOISTURE_NOISE ? lost : 0;

  const waterFactor = Math.max(
    0.5,
    1 - WATER_SLOPE * Math.max(0, waterCementitiousRatio(composition) - WATER_REFERENCE),
  );
  const moistureFactor = Math.max(0.5, 1 - MOISTURE_SLOPE * effectiveLoss);
  const scmFactor = 1 - ((SCM_DELAY * scmMass) / (cementMass + scmMass)) * Math.max(0, (28 - day) / 25);

  const age = equivalentAge(batch.CastAt, specimen.CuringReadings, day, calibration);
  const coefficients = calibration.CementTypeCalibrations[batch.CementType];
  const baseline = age / (coefficients.TypeACoefficient + coefficients.TypeBCoefficient * age);

  return Math.round(
    batch.ExpectedFinalBreakPsi *
      baseline *
      waterFactor *
      moistureFactor *
      scmFactor *
      scatter(batch.BatchNumber, specimen.Label),
  );
}

export function generate(source, calibration) {
  const result = structuredClone(source);
  for (const batch of result.Batches) {
    const readings = batch.Specimens.flatMap(specimen => specimen.CuringReadings);

    // Applied once: an already-cold batch has every ambient reading below 60 °F.
    if (batch.BatchNumber === COLD_CASE && readings.some(reading => reading.AmbientTempF >= 60)) {
      for (const reading of readings) {
        reading.InternalTempF = round1(reading.InternalTempF - COLD_SHIFT_F);
        reading.AmbientTempF = round1(reading.AmbientTempF - COLD_SHIFT_F);
      }
    }

    // Applied once: skipped when the batch already loses more than MOISTURE_NOISE points.
    if (batch.BatchNumber === MOISTURE_CASE && moistureLoss(batch) < MOISTURE_NOISE) {
      for (const reading of readings) {
        const hours = hoursAfterCast(batch, reading);
        reading.MoisturePct = round1(reading.MoisturePct - MOISTURE_DROP * Math.max(0, Math.min(1, (hours - 24) / 48)));
      }
    }

    for (const specimen of batch.Specimens) {
      specimen.BreakResult.StrengthPsi = calculateBreak(batch, specimen, calibration);
    }
  }
  return result;
}

/** Throws if any value would be rejected by the configured limits. */
export function validate(dataset, limits) {
  for (const batch of dataset.Batches) {
    for (const [key, value] of valuesToCheck(batch)) {
      const reject = limits.find(limit => limit.Key === key && limit.Severity === 'Reject');
      if (reject && outside(value, reject)) {
        throw new Error(`${batch.BatchNumber}: ${key} rejects ${value}`);
      }
    }
  }
}

/**
 * The saved warnings loading the dataset produces, as { batch: { key: count } }. The 3,000 PSI break
 * warning applies to 28-day breaks only (client-decisions §3, team proposal pending client confirmation).
 */
export function flags(dataset, limits) {
  const result = {};
  for (const batch of dataset.Batches) {
    const counts = {};
    for (const [key, value, warns = true] of valuesToCheck(batch)) {
      if (!warns) continue;
      const warn = limits.find(limit => limit.Key === key && limit.Severity === 'Warn');
      if (warn && outside(value, warn)) counts[key] = (counts[key] ?? 0) + 1;
    }
    result[batch.BatchNumber] = counts;
  }
  return result;
}

/** The demo cases: one on-track batch, three at-risk batches for different reasons, and the pH case. */
export const INTENDED_FLAGS = {
  'VM-SYN-001': {},
  'VM-SYN-002': { 'Curing.AmbientTempF': 63 },
  'VM-SYN-003': {},
  'VM-SYN-004': {},
  'VM-SYN-005': {},
  'VM-SYN-006': { 'Composition.WaterCementRatio': 1 },
  'VM-SYN-007': {},
  'VM-SYN-008': {},
  'VM-SYN-009': { 'Batch.MixWaterPh': 1 },
};

function outside(value, limit) {
  return (limit.Min !== null && value < limit.Min) || (limit.Max !== null && value > limit.Max);
}

function* valuesToCheck(batch) {
  yield ['Batch.MixWaterPh', batch.MixWaterPh];
  if (batch.CementSurfacePh !== null) yield ['Batch.CementSurfacePh', batch.CementSurfacePh];
  yield ['Batch.FinalWeightLb', batch.FinalWeightLb];
  yield ['Composition.WaterCementRatio', Math.round(waterCementitiousRatio(batch.Composition) * 1000) / 1000];
  const oxides = batch.CementOxideAnalysis;
  yield ['Oxide.AluminaPct', oxides.AluminaPct];
  yield ['Oxide.IronOxidePct', oxides.IronOxidePct];
  yield ['Oxide.MagnesiumOxidePct', oxides.MagnesiumOxidePct];
  yield ['Oxide.SulfurTrioxidePct', oxides.SulfurTrioxidePct];
  for (const specimen of batch.Specimens) {
    for (const reading of specimen.CuringReadings) {
      yield ['Curing.InternalTempF', reading.InternalTempF];
      yield ['Curing.AmbientTempF', reading.AmbientTempF];
      yield ['Curing.MoisturePct', reading.MoisturePct];
      yield ['Curing.RelativeHumidityPct', reading.RelativeHumidityPct];
      yield ['Curing.ResistivityOhmPerFt', reading.ResistivityOhmPerFt];
      yield ['Curing.Co2Ppm', reading.Co2Ppm];
    }
    if (specimen.BreakResult) {
      // Rejection limits apply to every break; the warning only to 28-day breaks.
      yield ['Break.StrengthPsi', specimen.BreakResult.StrengthPsi, specimen.TestAgeDays === 'Day28'];
    }
  }
}

function main() {
  const args = process.argv.slice(2);
  if (args.some(argument => argument !== '--check')) {
    throw new Error('Usage: node data/synthetic/generate.mjs [--check]');
  }
  const readJson = relative =>
    JSON.parse(readFileSync(new URL(relative, import.meta.url), 'utf8').replace(/^﻿/, ''));
  const source = readJson('./batches.json');
  const calibration = readJson('../../src/Vulcan.Infrastructure/Seed/calibration-v1.json').ParametersJson.Base;
  const limits = readJson('../../src/Vulcan.Infrastructure/Seed/validation-limits.json');
  const generated = generate(source, calibration);
  validate(generated, limits);
  if (!isDeepStrictEqual(flags(generated, limits), INTENDED_FLAGS)) {
    throw new Error(`Unexpected warnings: ${JSON.stringify(flags(generated, limits))}`);
  }
  if (args.includes('--check')) {
    if (!isDeepStrictEqual(source, generated)) {
      throw new Error('batches.json differs from the generator output. Run: node data/synthetic/generate.mjs');
    }
    console.log('batches.json matches the generator; rejection limits pass; warnings match the demo cases.');
  } else {
    writeFileSync(new URL('./batches.json', import.meta.url), `${JSON.stringify(generated, null, 2)}\n`);
    console.log(`Generated ${generated.Batches.length} synthetic batches.`);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
