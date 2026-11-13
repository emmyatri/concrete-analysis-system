import { MOCK_BATCH_DETAILS } from './mock/mock-batch-details';
import { mockStrength } from './mock/mock-strength';
import { BatchStrength, EarlyWarning, SavedWarning } from './models';
import { buildReportRecommendations } from './report-recommendations';

describe('buildReportRecommendations', () => {
  const batch = MOCK_BATCH_DETAILS.find((item) => item.batchNumber === 'VM-SYN-006')!;

  const baseStrength = mockStrength(batch);

  const strengthWith = (overrides: Partial<BatchStrength> = {}): BatchStrength => ({
    ...baseStrength,
    atRisk: false,
    status: 'Estimated',
    earlyWarnings: [],
    ...overrides,
  });

  const savedWarning = (limitKey: string): SavedWarning => ({
    id: 1,
    entityType: 'Batch',
    entityId: batch.id,
    fieldName: limitKey,
    limitKey,
    value: 1,
    message: 'Test validation warning',
    createdAtUtc: '2026-11-13T12:00:00Z',
  });

  const earlyWarning = (limitKey: string): EarlyWarning => ({
    warning: 'Test early-strength warning',
    field: limitKey,
    value: 1,
    limitKey,
    limit: 1,
  });

  const codes = (strength: BatchStrength, warnings: SavedWarning[] = []): string[] =>
    buildReportRecommendations(strength, warnings).map((recommendation) => recommendation.code);

  it('recommends reviewing projected strength when the batch is at risk', () => {
    expect(codes(strengthWith({ atRisk: true }))).toContain('strength-risk');
  });

  it('recommends checking curing data when readings are insufficient', () => {
    expect(codes(strengthWith({ status: 'InsufficientCuringData' }))).toContain('curing-data');
  });

  it('recognizes recorded water-to-cementitious ratio warnings', () => {
    expect(codes(strengthWith(), [savedWarning('Composition.WaterCementRatio')])).toContain(
      'water-cement-ratio',
    );
  });

  it('recognizes warning codes from the early-strength engine', () => {
    expect(
      codes(
        strengthWith({
          earlyWarnings: [
            earlyWarning('Composition.WaterCementRatio'),
            earlyWarning('Curing.AmbientTempF'),
          ],
        }),
      ),
    ).toEqual(['water-cement-ratio', 'curing-temperature']);
  });

  it('recognizes moisture and break-strength warnings', () => {
    expect(
      codes(strengthWith(), [
        savedWarning('Curing.MoisturePct'),
        savedWarning('Break.StrengthPsi'),
      ]),
    ).toEqual(['curing-moisture', 'break-strength']);
  });

  it('groups flagged pH measurements into one recommendation', () => {
    expect(
      codes(strengthWith(), [
        savedWarning('Batch.MixWaterPh'),
        savedWarning('Batch.CementSurfacePh'),
      ]),
    ).toEqual(['ph-review']);
  });

  it('groups flagged oxide measurements into one recommendation', () => {
    expect(
      codes(strengthWith(), [
        savedWarning('Oxide.AluminaPct'),
        savedWarning('Oxide.MagnesiumOxidePct'),
      ]),
    ).toEqual(['cement-chemistry']);
  });

  it('does not duplicate recommendations across warning sources', () => {
    expect(
      codes(
        strengthWith({
          earlyWarnings: [earlyWarning('Composition.WaterCementRatio')],
        }),
        [
          savedWarning('Composition.WaterCementRatio'),
          savedWarning('Composition.WaterCementRatio'),
        ],
      ),
    ).toEqual(['water-cement-ratio']);
  });

  it('provides general guidance for an unrecognized warning', () => {
    expect(codes(strengthWith(), [savedWarning('Unknown.WarningCode')])).toEqual([
      'validation-review',
    ]);
  });

  it('provides general guidance for an unrecognized early warning', () => {
    expect(
      codes(
        strengthWith({
          earlyWarnings: [earlyWarning('Unknown.WarningCode')],
        }),
      ),
    ).toEqual(['validation-review']);
  });

  it('returns no recommendations when there are no warning conditions', () => {
    expect(codes(strengthWith())).toEqual([]);
  });

  it('preserves strength risk and mix recommendations together', () => {
    expect(
      codes(strengthWith({ atRisk: true }), [savedWarning('Composition.WaterCementRatio')]),
    ).toEqual(['strength-risk', 'water-cement-ratio']);
  });
});
