import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { calculateBreak, generate, scatter, validate } from './generate.mjs';

const readJson = relative => JSON.parse(readFileSync(new URL(relative, import.meta.url), 'utf8').replace(/^\uFEFF/, ''));
const source = readJson('./batches.json');
const calibration = readJson('../../src/Vulcan.Infrastructure/Seed/calibration-v1.json').ParametersJson.Base;
const limits = readJson('../../src/Vulcan.Infrastructure/Seed/validation-limits.json');

function example(day = 'Day28') {
  const batch = structuredClone(source.Batches.find(candidate => candidate.BatchNumber === 'VM-SYN-001'));
  const specimen = batch.Specimens.find(candidate => candidate.TestAgeDays === day);
  return { batch, specimen };
}

test('generation is repeatable, order-independent, and leaves source data untouched', () => {
  const original = structuredClone(source);
  const generated = generate(source, calibration);
  assert.deepEqual(generate(source, calibration), generated);
  assert.deepEqual(generate(generated, calibration), generated);
  const reversed = structuredClone(source);
  reversed.Batches.reverse();
  assert.deepEqual(generate(reversed, calibration).Batches.reverse(), generated.Batches);
  assert.deepEqual(source, original);
});

test('only the approved curing measurements and break strengths change', () => {
  const generated = generate(source, calibration);
  for (const [index, batch] of generated.Batches.entries()) {
    const previous = source.Batches[index];
    const { Specimens: previousSpecimens, ...previousBatch } = previous;
    const { Specimens: currentSpecimens, ...currentBatch } = batch;
    assert.deepEqual(currentBatch, previousBatch);
    assert.equal(currentSpecimens.length, previousSpecimens.length);
    for (const [specimenIndex, specimen] of currentSpecimens.entries()) {
      const restored = structuredClone(specimen);
      const oldSpecimen = previousSpecimens[specimenIndex];
      restored.BreakResult.StrengthPsi = oldSpecimen.BreakResult.StrengthPsi;
      for (const [readingIndex, reading] of restored.CuringReadings.entries()) {
        for (const field of ['InternalTempF', 'AmbientTempF', 'MoisturePct']) {
          reading[field] = oldSpecimen.CuringReadings[readingIndex][field];
        }
      }
      assert.deepEqual(restored, oldSpecimen);
    }
  }
});

test('colder curing alone lowers generated strength', () => {
  const { batch, specimen } = example();
  const warm = calculateBreak(batch, specimen, calibration);
  for (const reading of specimen.CuringReadings) reading.InternalTempF -= 20;
  assert.ok(calculateBreak(batch, specimen, calibration) < warm);
});

test('higher water ratio alone lowers strength above the reference ratio', () => {
  const { batch, specimen } = example();
  const normal = calculateBreak(batch, specimen, calibration);
  batch.Composition.WaterPct = 19;
  assert.ok(calculateBreak(batch, specimen, calibration) < normal);
});

test('day-1 to day-3 moisture loss alone lowers later strength', () => {
  const { batch, specimen } = example();
  const normal = calculateBreak(batch, specimen, calibration);
  specimen.CuringReadings.at(-1).MoisturePct -= 2;
  assert.ok(calculateBreak(batch, specimen, calibration) < normal);
});

test('SCM alone delays early strength and has no day-28 multiplier', () => {
  for (const day of ['Day3', 'Day7', 'Day28']) {
    const { batch, specimen } = example(day);
    const normal = calculateBreak(batch, specimen, calibration);
    const ratio = batch.Composition.WaterPct / (batch.Composition.CementPct * 3.15);
    batch.Composition.ScmType = 'Slag';
    batch.Composition.ScmPct = 3;
    batch.Composition.WaterPct = ratio * (batch.Composition.CementPct * 3.15 + 3 * 2.90);
    const withScm = calculateBreak(batch, specimen, calibration);
    if (day === 'Day28') assert.equal(withScm, normal);
    else assert.ok(withScm < normal);
  }
});

test('deterministic scatter stays within five percent for every specimen', () => {
  const values = source.Batches.flatMap(batch => batch.Specimens.map(specimen => {
    const value = scatter(batch.BatchNumber, specimen.Label);
    assert.equal(value, scatter(batch.BatchNumber, specimen.Label));
    assert.ok(value >= 0.95 && value <= 1.05);
    return value;
  }));
  assert.ok(values.some(value => value < 1));
  assert.ok(values.some(value => value > 1));
});

test('generated values obey the existing rejection limits and reject invalid measurements', () => {
  const generated = generate(source, calibration);
  assert.doesNotThrow(() => validate(generated, limits));
  const rule = limits.find(limit => limit.Key === 'Curing.InternalTempF' && limit.Severity === 'Reject');
  generated.Batches[0].Specimens[0].CuringReadings[0].InternalTempF = rule.Max + 0.1;
  assert.throws(() => validate(generated, limits), /Curing.InternalTempF/);
  generated.Batches[0].Specimens[0].CuringReadings[0].InternalTempF = Number.NaN;
  assert.throws(() => validate(generated, limits), /Curing.InternalTempF/);
});
