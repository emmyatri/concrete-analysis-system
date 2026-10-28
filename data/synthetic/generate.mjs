import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { isDeepStrictEqual } from 'node:util';
import { pathToFileURL } from 'node:url';

const SEED = 166;
const WATER_REFERENCE = 0.50;
const WATER_SLOPE = 2.0;
const MOISTURE_SLOPE = 0.12;
const SCM_DELAY = 0.50;
const SPECIFIC_GRAVITIES = { None: 0, FlyAsh: 2.35, Slag: 2.90, SilicaFume: 2.35 };
const HOUR_MS = 3_600_000;

export function scatter(batchNumber, label) {
  const hash = createHash('sha256').update(`${SEED}:${batchNumber}:${label}`).digest();
  return 0.95 + 0.10 * (hash.readUInt32BE(0) / 2 ** 32);
}

export function equivalentAge(castAt, readings, day, calibration) {
  const points = readings.map(reading => ({
    hours: (Date.parse(reading.TakenAt) - Date.parse(castAt)) / HOUR_MS,
    temperature: reading.InternalTempF,
  })).sort((left, right) => left.hours - right.hours);
  const targetHours = day * 24;
  const applicable = points.filter(point => point.hours <= targetHours);
  const datum = calibration.DatumTemperatureF;
  let maturity = Math.max(0, points[0].temperature - datum) * Math.min(points[0].hours, targetHours);
  for (let index = 1; index < applicable.length; index++) {
    const previous = applicable[index - 1];
    const current = applicable[index];
    maturity += Math.max(0, (previous.temperature + current.temperature) / 2 - datum)
      * (current.hours - previous.hours);
  }
  const last = applicable.at(-1);
  if (last && last.hours < targetHours) {
    const following = points.find(point => point.hours > targetHours);
    let average = applicable.reduce((sum, point) => sum + point.temperature, 0) / applicable.length;
    if (following) {
      const interpolated = last.temperature + (following.temperature - last.temperature)
        * (targetHours - last.hours) / (following.hours - last.hours);
      average = (last.temperature + interpolated) / 2;
    }
    maturity += Math.max(0, average - datum) * (targetHours - last.hours);
  }
  return maturity / ((calibration.ReferenceTemperatureF - datum) * 24);
}

export function calculateBreak(batch, specimen, calibration) {
  const composition = batch.Composition;
  const cementMass = composition.CementPct * 3.15;
  const scmMass = composition.ScmPct * SPECIFIC_GRAVITIES[composition.ScmType];
  const binderMass = cementMass + scmMass;
  const ratio = composition.WaterPct / binderMass;
  const day = Number(specimen.TestAgeDays.slice(3));
  const readingAt = hours => specimen.CuringReadings.find(reading =>
    (Date.parse(reading.TakenAt) - Date.parse(batch.CastAt)) / HOUR_MS === hours);
  const loss = Math.max(0, readingAt(24).MoisturePct - readingAt(72).MoisturePct);
  const waterFactor = Math.max(0.50, 1 - WATER_SLOPE * Math.max(0, ratio - WATER_REFERENCE));
  const moistureFactor = Math.max(0.50, 1 - MOISTURE_SLOPE * loss * Math.min(day / 7, 1));
  const scmFactor = 1 - SCM_DELAY * scmMass / binderMass * Math.max(0, (28 - day) / 25);
  const age = equivalentAge(batch.CastAt, specimen.CuringReadings, day, calibration);
  const coefficients = calibration.CementTypeCalibrations[batch.CementType];
  const baseline = age / (coefficients.TypeACoefficient + coefficients.TypeBCoefficient * age);
  return Math.round(batch.ExpectedFinalBreakPsi * baseline * waterFactor * moistureFactor * scmFactor
    * scatter(batch.BatchNumber, specimen.Label));
}

export function generate(source, calibration) {
  const result = structuredClone(source);
  for (const batch of result.Batches) {
    const cold = batch.BatchNumber === 'VM-SYN-002';
    const moistureLoss = batch.BatchNumber === 'VM-SYN-003';
    for (const specimen of batch.Specimens) {
      const replicate = Number(specimen.Label.split('-').at(-1)) - 2;
      for (const reading of specimen.CuringReadings) {
        const hours = (Date.parse(reading.TakenAt) - Date.parse(batch.CastAt)) / HOUR_MS;
        reading.InternalTempF = Math.round(((cold ? 35 : 73.4) + replicate * 0.2) * 10) / 10;
        reading.AmbientTempF = cold ? 45 : 73.4;
        const loss = moistureLoss ? 2 : 0;
        reading.MoisturePct = Math.round((4.5 - loss * Math.max(0, Math.min(1, (hours - 24) / 48))) * 100) / 100;
      }
      specimen.BreakResult.StrengthPsi = calculateBreak(batch, specimen, calibration);
    }
  }
  return result;
}

export function validate(dataset, limits) {
  const groups = {
    Batch: batch => [batch],
    Composition: batch => [batch.Composition],
    Oxide: batch => [batch.CementOxideAnalysis],
    Specimen: batch => batch.Specimens,
    Break: batch => batch.Specimens.map(specimen => specimen.BreakResult),
    Curing: batch => batch.Specimens.flatMap(specimen => specimen.CuringReadings),
  };
  for (const batch of dataset.Batches) {
    for (const rule of limits.filter(limit => limit.Severity === 'Reject')) {
      const [prefix, field] = rule.Key.split('.');
      if (!groups[prefix]) continue;
      for (const entity of groups[prefix](batch)) {
        if (field !== 'TotalPct' && !Object.hasOwn(entity, field)) continue;
        const value = field === 'TotalPct'
          ? ['CementPct', 'ScmPct', 'CoarseAggregatePct', 'FineAggregatePct', 'WaterPct']
            .reduce((sum, name) => sum + entity[name], 0)
          : entity[field];
        if (!Number.isFinite(value) || (rule.Min !== null && value < rule.Min)
          || (rule.Max !== null && value > rule.Max)) {
          throw new Error(`${batch.BatchNumber}: ${rule.Key} rejects ${value}`);
        }
      }
    }
  }
}

function main() {
  const args = process.argv.slice(2);
  if (args.some(argument => argument !== '--check')) {
    throw new Error('Usage: node data/synthetic/generate.mjs [--check]');
  }
  const readJson = relative => JSON.parse(readFileSync(new URL(relative, import.meta.url), 'utf8').replace(/^\uFEFF/, ''));
  const source = readJson('./batches.json');
  const calibration = readJson('../../src/Vulcan.Infrastructure/Seed/calibration-v1.json').ParametersJson.Base;
  const limits = readJson('../../src/Vulcan.Infrastructure/Seed/validation-limits.json');
  const generated = generate(source, calibration);
  validate(generated, limits);
  if (args.includes('--check')) {
    if (!isDeepStrictEqual(source, generated)) {
      throw new Error('Synthetic readings or breaks differ from the reproducible generator output.');
    }
    console.log('Synthetic readings and breaks match the generator; all rejection limits pass.');
  } else {
    writeFileSync(new URL('./batches.json', import.meta.url), `${JSON.stringify(generated, null, 2)}\n`);
    console.log(`Generated ${generated.Batches.length} synthetic batches.`);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
