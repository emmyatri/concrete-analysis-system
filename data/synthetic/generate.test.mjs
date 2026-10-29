// Run with: node --test data/synthetic/generate.test.mjs
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import {
  INTENDED_FLAGS,
  MOISTURE_NOISE,
  calculateBreak,
  flags,
  generate,
  moistureLoss,
  scatter,
  validate,
} from './generate.mjs';

const readJson = relative =>
  JSON.parse(readFileSync(new URL(relative, import.meta.url), 'utf8').replace(/^﻿/, ''));
const dataset = readJson('./batches.json');
const calibration = readJson('../../src/Vulcan.Infrastructure/Seed/calibration-v1.json').ParametersJson.Base;
const limits = readJson('../../src/Vulcan.Infrastructure/Seed/validation-limits.json');

const batch = number => structuredClone(dataset.Batches.find(candidate => candidate.BatchNumber === number));
const specimen = (b, age) => b.Specimens.find(candidate => candidate.TestAgeDays === age);

test('the committed data matches the generator, and regenerating changes nothing', () => {
  assert.deepEqual(generate(dataset, calibration), dataset);
});

test('values stay inside every rejection limit', () => {
  assert.doesNotThrow(() => validate(dataset, limits));
});

test('loading the data flags only the demo cases', () => {
  assert.deepEqual(flags(dataset, limits), INTENDED_FLAGS);
});

test('colder curing alone lowers strength', () => {
  const onTrack = batch('VM-SYN-001');
  const cold = structuredClone(onTrack);
  for (const reading of specimen(cold, 'Day28').CuringReadings) reading.InternalTempF -= 20;
  assert.ok(calculateBreak(cold, specimen(cold, 'Day28'), calibration) < calculateBreak(onTrack, specimen(onTrack, 'Day28'), calibration));
});

test('a higher water ratio alone lowers strength', () => {
  const normal = batch('VM-SYN-001');
  const wet = structuredClone(normal);
  wet.Composition.WaterPct += 4;
  assert.ok(calculateBreak(wet, specimen(wet, 'Day28'), calibration) < calculateBreak(normal, specimen(normal, 'Day28'), calibration));
});

test('moisture loss lowers strength only above normal variation', () => {
  const moist = batch('VM-SYN-003');
  assert.ok(moistureLoss(moist) >= MOISTURE_NOISE);
  const kept = structuredClone(moist);
  for (const s of kept.Specimens) for (const r of s.CuringReadings) r.MoisturePct = 90;
  assert.ok(calculateBreak(moist, specimen(moist, 'Day28'), calibration) < calculateBreak(kept, specimen(kept, 'Day28'), calibration));
  assert.ok(moistureLoss(batch('VM-SYN-001')) < MOISTURE_NOISE);
});

test('SCM delays early strength and leaves day 28 unchanged', () => {
  const withScm = batch('VM-SYN-007');
  const without = structuredClone(withScm);
  without.Composition.ScmPct = 0;
  without.Composition.ScmType = 'None';
  without.Composition.CementPct += withScm.Composition.ScmPct * (2.9 / 3.15);
  const day3 = s => calculateBreak(s, specimen(s, 'Day3'), calibration);
  assert.ok(day3(withScm) < day3(without));
  const day28 = s => calculateBreak(s, specimen(s, 'Day28'), calibration);
  assert.ok(Math.abs(day28(withScm) - day28(without)) <= 1);
});

test('scatter is deterministic and within ±5%', () => {
  const values = dataset.Batches.flatMap(b => b.Specimens.map(s => scatter(b.BatchNumber, s.Label)));
  assert.ok(values.every(value => value >= 0.95 && value <= 1.05));
  assert.equal(scatter('VM-SYN-001', 'D3-1'), scatter('VM-SYN-001', 'D3-1'));
  assert.ok(values.some(value => value < 1) && values.some(value => value > 1));
});
