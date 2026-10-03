import { BatchSummary, SpecificGravity, ValidationLimit } from '../models';

/**
 * Fake data for development builds only (environment.useMocks). Never shipped in the desktop app.
 * Limits and gravities are the same as the SCRUM-96 seed files (Vulcan.Infrastructure/Seed).
 */

export const MOCK_VALIDATION_LIMITS: ValidationLimit[] = [
  { key: 'Batch.FinalWeightLb', severity: 'Reject', min: 0.01, max: null, unit: 'lb', message: 'Weight must be greater than 0', source: 'schema.md' },
  { key: 'Batch.ExpectedFinalBreakPsi', severity: 'Reject', min: 3000, max: 10000, unit: 'PSI', message: 'PSI must be within the range [3000-10,000]', source: 'Client doc / FR-5' },
  { key: 'Batch.MixWaterPh', severity: 'Reject', min: 1, max: 9, unit: '', message: 'pH must be within the range [1-9]', source: 'Client doc / FR-7, Decision 4' },
  { key: 'Batch.MixWaterPh', severity: 'Warn', min: 7, max: 7.5, unit: '', message: 'pH is outside the normal range [7.0-7.5]', source: 'Client doc ("normal")' },
  { key: 'Composition.CementPct', severity: 'Reject', min: 10, max: 15, unit: '%', message: 'Cement composition must be within the range [10-15]', source: 'Client doc / FR-2.1' },
  { key: 'Composition.ScmPct', severity: 'Reject', min: 0, max: 100, unit: '%', message: 'SCM composition must be a valid percentage [0-100]', source: 'FR-2' },
  { key: 'Composition.FineAggregatePct', severity: 'Reject', min: 25, max: 30, unit: '%', message: 'Fine aggregate composition must be within the range [25-30]', source: 'Client doc / FR-2.1' },
  { key: 'Composition.CoarseAggregatePct', severity: 'Reject', min: 40, max: 55, unit: '%', message: 'Coarse aggregate composition must be within the range [40-55]', source: 'Client doc / FR-2.1' },
  { key: 'Composition.WaterPct', severity: 'Reject', min: 15, max: 20, unit: '%', message: 'Water composition must be within the range [15-20]', source: 'Client doc / FR-2.1' },
  { key: 'Composition.TotalPct', severity: 'Reject', min: null, max: 100, unit: '%', message: 'Total composition must not exceed 100%', source: 'Decision 2 (NRMCA)' },
  { key: 'Composition.WaterCementRatio', severity: 'Warn', min: 0.4, max: 0.6, unit: 'ratio', message: 'The water to cement ratio is outside the normal range [0.4-0.6]', source: 'Client doc, Hydration' },
  { key: 'Oxide.AluminaPct', severity: 'Warn', min: 3.0, max: 7.5, unit: '%', message: 'The aluminum oxide percent is outside the normal range [3.0-7.5]', source: 'Client doc table' },
  { key: 'Oxide.AluminaPct', severity: 'Reject', min: 0, max: 100, unit: '%', message: 'The aluminum oxide percent must be a valid percentage [0-100]', source: 'FR-3' },
  { key: 'Oxide.IronOxidePct', severity: 'Warn', min: 0.5, max: 6.0, unit: '%', message: 'The iron oxide percent is outside the normal range [0.5-6.0]', source: 'Client doc table' },
  { key: 'Oxide.IronOxidePct', severity: 'Reject', min: 0, max: 100, unit: '%', message: 'The iron oxide percent must be a valid percentage [0-100]', source: 'FR-3' },
  { key: 'Oxide.MagnesiumOxidePct', severity: 'Warn', min: 0.1, max: 4.0, unit: '%', message: 'The magnesium oxide percent is outside the normal range [0.1-4.0]', source: 'Client doc table' },
  { key: 'Oxide.MagnesiumOxidePct', severity: 'Reject', min: 0, max: 6.0, unit: '%', message: 'The magnesium oxide percent must be within the range [0.0-6.0]', source: 'ASTM C150' },
  { key: 'Oxide.SulfurTrioxidePct', severity: 'Warn', min: 1.3, max: 3.0, unit: '%', message: 'The sulfur trioxide percent is outside the normal range [1.3-3.0]', source: 'Client doc table' },
  { key: 'Oxide.SulfurTrioxidePct', severity: 'Reject', min: 0, max: 4.5, unit: '%', message: 'The sulfur trioxide percent must be within the range [0.0-4.5]', source: 'ASTM C150 Table 1 (Type III, C3A > 8%)' },
  { key: 'Specimen.LengthIn', severity: 'Reject', min: 0.01, max: 12, unit: 'in', message: 'Specimen length must be nonzero and in the range [0-12]', source: 'FR-4' },
  { key: 'Specimen.WidthIn', severity: 'Reject', min: 0.01, max: 6.0, unit: 'in', message: 'Specimen width must be nonzero and in the range [0-6]', source: 'FR-4' },
  { key: 'Break.StrengthPsi', severity: 'Reject', min: 1, max: 10000, unit: 'PSI', message: 'PSI must be within the range [1-10000]', source: 'Client doc / FR-5, Decision 3' },
  { key: 'Break.StrengthPsi', severity: 'Warn', min: 3000, max: 10000, unit: 'PSI', message: 'Break strength is below 3,000 PSI. Saved and flagged for review.', source: 'Client doc / FR-5' },
  { key: 'Curing.InternalTempF', severity: 'Reject', min: 0, max: 200, unit: '°F', message: 'Internal temperature must be within the range [0-200]', source: 'Plausibility (heat of hydration)' },
  { key: 'Curing.AmbientTempF', severity: 'Warn', min: 60, max: 80, unit: '°F', message: 'Ambient temperature is outside the normal range [60-80]', source: 'ASTM C31 initial curing' },
  { key: 'Curing.AmbientTempF', severity: 'Reject', min: 0, max: 150, unit: '°F', message: 'Ambient temperature must be within the range [0-150]', source: 'Plausibility' },
  { key: 'Curing.MoisturePct', severity: 'Reject', min: 0, max: 100, unit: '%', message: 'Moisture percent must be a valid percentage [0-100]', source: 'FR-6' },
  { key: 'Curing.RelativeHumidityPct', severity: 'Reject', min: 0, max: 100, unit: '%', message: 'Relative humidity percent must be a valid percentage [0-100]', source: 'FR-6' },
  { key: 'Curing.ResistivityOhmPerFt', severity: 'Reject', min: 0.01, max: null, unit: 'ohms/ft', message: 'Resistivity must be a positive number', source: 'FR-6' },
  { key: 'Curing.Co2Ppm', severity: 'Reject', min: 0, max: null, unit: 'ppm', message: 'CO2 ppm must be non-negative', source: 'FR-6' },
];

export const MOCK_SPECIFIC_GRAVITIES: SpecificGravity[] = [
  { material: 'Cement', value: 3.15 },
  { material: 'Water', value: 1.0 },
  { material: 'Slag', value: 2.9 },
  { material: 'FlyAsh', value: 2.35 },
  { material: 'SilicaFume', value: 2.35 },
];

/** The three batches from the hi-fi mockup (Home), plus synthetic ones for search and the toggle. */
export const MOCK_BATCHES: BatchSummary[] = [
  { id: 142, batchNumber: 'VM-2026-0142', castAt: '2026-09-29T11:40:00Z', cementType: 'TypeIII', expectedFinalBreakPsi: 6000, waterCementRatio: 0.31, cylinderCount: 6, breaksRecorded: 0, average28DayPsi: null, warningCount: 1, isSynthetic: false },
  { id: 141, batchNumber: 'VM-2026-0141', castAt: '2026-09-26T11:30:00Z', cementType: 'TypeI', expectedFinalBreakPsi: 6200, waterCementRatio: 0.45, cylinderCount: 9, breaksRecorded: 3, average28DayPsi: null, warningCount: 0, isSynthetic: false },
  { id: 139, batchNumber: 'VM-2026-0139', castAt: '2026-09-22T11:15:00Z', cementType: 'TypeII', expectedFinalBreakPsi: 5800, waterCementRatio: 0.48, cylinderCount: 9, breaksRecorded: 3, average28DayPsi: null, warningCount: 0, isSynthetic: false },
  { id: 903, batchNumber: 'VM-SYN-0003', castAt: '2026-08-14T12:00:00Z', cementType: 'TypeI', expectedFinalBreakPsi: 5000, waterCementRatio: 0.52, cylinderCount: 9, breaksRecorded: 9, average28DayPsi: 5180, warningCount: 0, isSynthetic: true },
  { id: 902, batchNumber: 'VM-SYN-0002', castAt: '2026-08-07T12:00:00Z', cementType: 'TypeIII', expectedFinalBreakPsi: 7000, waterCementRatio: 0.38, cylinderCount: 9, breaksRecorded: 9, average28DayPsi: 2950, warningCount: 1, isSynthetic: true },
  { id: 901, batchNumber: 'VM-SYN-0001', castAt: '2026-07-31T12:00:00Z', cementType: 'TypeII', expectedFinalBreakPsi: 5500, waterCementRatio: 0.47, cylinderCount: 9, breaksRecorded: 9, average28DayPsi: 5620, warningCount: 0, isSynthetic: true },
];
