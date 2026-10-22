import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { USE_MOCKS } from '../use-mocks';
import { VulcanApiService } from '../vulcan-api.service';
import { clearSavedMockBatches, mockApiInterceptor } from './mock-api.interceptor';

describe('mockApiInterceptor', () => {
  beforeEach(() => clearSavedMockBatches());
  const setUp = (useMocks: boolean) => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        provideHttpClientTesting(),
        { provide: USE_MOCKS, useValue: useMocks },
      ],
    });
    return TestBed.inject(VulcanApiService);
  };

  it('mock mode returns the fake batches, newest first', async () => {
    const api = setUp(true);

    const batches = await firstValueFrom(api.getBatches());

    expect(batches.slice(0, 3).map((b) => b.batchNumber)).toEqual([
      'VM-2026-0142',
      'VM-2026-0141',
      'VM-2026-0139',
    ]);
  });

  it('finds batches by any part of the number, ignoring upper/lowercase', async () => {
    const api = setUp(true);

    const lower = await firstValueFrom(api.getBatches({ search: 'vm-syn' }));
    const upper = await firstValueFrom(api.getBatches({ search: 'VM-SYN' }));

    expect(lower).toHaveLength(9);
    expect(lower[0].batchNumber).toBe('VM-SYN-009');
    expect(upper).toEqual(lower);
  });

  it('hides synthetic batches when asked', async () => {
    const api = setUp(true);

    const batches = await firstValueFrom(api.getBatches({ includeSynthetic: false }));

    expect(batches.some((b) => b.isSynthetic)).toBe(false);
  });

  it('returns the validation limits and specific gravities from the seed', async () => {
    const api = setUp(true);

    const limits = await firstValueFrom(api.getValidationLimits());
    const gravities = await firstValueFrom(api.getSpecificGravities());

    expect(limits.find((l) => l.key === 'Composition.CementPct')).toMatchObject({
      severity: 'Reject',
      min: 10,
      max: 15,
    });
    expect(gravities.find((g) => g.material === 'Cement')?.value).toBe(3.15);
  });

  it('rejects a duplicate batch number with a 400 the screens can show', async () => {
    setUp(true);

    const error = await firstValueFrom(
      TestBed.inject(HttpClient).post('/api/batches', { batch: { batchNumber: 'vm-2026-0142' } }),
    ).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(HttpErrorResponse);
    expect((error as HttpErrorResponse).error.errors['Batch.BatchNumber']).toEqual([
      'Batch number vm-2026-0142 already exists',
    ]);
  });

  it('saves a batch with an unusual value and returns it as a warning', async () => {
    setUp(true);

    const created = await firstValueFrom(
      TestBed.inject(HttpClient).post<{ id: number; warnings: { key: string }[] }>('/api/batches', {
        batch: { batchNumber: 'VM-TEST-0001', mixWaterPh: 7.8 },
      }),
    );

    expect(created.id).toBeGreaterThan(0);
    expect(created.warnings.map((w) => w.key)).toEqual(['Batch.MixWaterPh']);
  });

  it('opens a batch on Batch Detail right after it is saved', async () => {
    const api = setUp(true);

    await firstValueFrom(
      api.createBatch({
        batch: {
          batchNumber: 'VM-TEST-0002',
          castAt: '2026-10-05T14:00:00.000Z',
          finalWeightLb: 25,
          expectedFinalBreakPsi: 6000,
          cementType: 'TypeI',
          mixWaterPh: 7.8,
          cementSurfacePh: 7.5,
        },
        composition: {
          cementPct: 10,
          waterPct: 17,
          coarseAggregatePct: 45,
          fineAggregatePct: 26,
          scmType: 'FlyAsh',
          scmPct: 2,
        },
        oxides: {
          aluminaPct: 4.8,
          ironOxidePct: 2.8,
          magnesiumOxidePct: 1.8,
          sulfurTrioxidePct: 2.2,
        },
      }),
    );
    const batch = await firstValueFrom(api.getBatch('vm-test-0002'));

    expect(batch.batchNumber).toBe('VM-TEST-0002');
    expect(batch.composition.totalPct).toBe(100);
    expect(batch.specimens).toEqual([]);
    expect(batch.warnings.map((w) => w.limitKey)).toEqual(['Batch.MixWaterPh']);
  });

  it('rejects a cylinder outside its size limits even when it skips the screen', async () => {
    const api = setUp(true);

    const error = await firstValueFrom(
      api.createSpecimen('VM-2026-0142', {
        label: 'Z9',
        lengthIn: 12.01,
        widthIn: 0,
        testAgeDays: 'Day3',
      }),
    ).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(HttpErrorResponse);
    expect(Object.keys((error as HttpErrorResponse).error.errors)).toEqual([
      'Specimen.LengthIn',
      'Specimen.WidthIn',
    ]);
  });

  it('returns a whole batch from the synthetic dataset, with its saved warnings', async () => {
    const api = setUp(true);

    const batch = await firstValueFrom(api.getBatch('vm-syn-006'));

    expect(batch.batchNumber).toBe('VM-SYN-006');
    expect(batch.specimens).toHaveLength(9);
    expect(batch.specimens.flatMap((s) => s.curingReadings)).toHaveLength(63);
    expect(batch.composition.waterCementRatio).toBe(0.603);
    expect(batch.warnings.map((w) => [w.limitKey, w.value])).toEqual([
      ['Composition.WaterCementRatio', 0.603],
      ['Break.StrengthPsi', 2950],
    ]);
  });

  it('every fake batch with warnings has the same count on Search and Batch Detail', async () => {
    const api = setUp(true);

    for (const summary of await firstValueFrom(api.getBatches())) {
      const detail = await firstValueFrom(api.getBatch(summary.batchNumber)).catch(() => null);
      if (detail)
        expect([summary.batchNumber, detail.warnings.length]).toEqual([
          summary.batchNumber,
          summary.warningCount,
        ]);
    }
  });

  it('answers an unknown batch number with a 404', async () => {
    const api = setUp(true);

    const error = await firstValueFrom(api.getBatch('VM-NOPE')).catch((e: unknown) => e);

    expect((error as HttpErrorResponse).status).toBe(404);
  });

  it('estimates an on-track batch from its readings and breaks', async () => {
    const api = setUp(true);

    const strength = await firstValueFrom(api.getStrength('vm-syn-001'));

    expect(strength).toMatchObject({ status: 'Estimated', atRisk: false, projected28DayPsi: 6408 });
    expect(strength.curvePoints.map((p) => p.day)).toEqual(
      Array.from({ length: 28 }, (_, i) => i + 1),
    );
    expect(strength.unadjustedCurvePoints).toHaveLength(28);
    expect(strength.estimates.map((e) => [e.horizonDays, e.adjustmentStage])).toEqual([
      [3, 'ThreeAndSevenDay'],
      [7, 'ThreeAndSevenDay'],
      [28, 'ThreeAndSevenDay'],
    ]);
    expect(strength.measuredBreaks).toHaveLength(9);
    expect(strength.curingPoints).toHaveLength(63);
    expect(strength.earlyWarnings).toEqual([]);
  });

  it('VM-SYN-006 looks on track until its 3- and 7-day breaks re-fit the curve', async () => {
    const api = setUp(true);

    const strength = await firstValueFrom(api.getStrength('VM-SYN-006'));

    // Same values as Vulcan.Prediction's tests at the 14 °F datum (SCRUM-194).
    expect(strength.estimates.map((e) => e.unadjustedPsi)).toEqual([3848, 5094, 6235]);
    expect(
      strength.curingPoints.filter((p) => p.specimenLabel === 'D28-1').at(-1)?.maturityFHours,
    ).toBe(4730.4);
    // After the re-fit (ADR-005 section 4.4) it's 28% short of the expected 6,200.
    expect(strength).toMatchObject({ atRisk: true, projected28DayPsi: 4449, shortfallPsi: 1751 });
    expect(strength.earlyWarnings.map((w) => w.limitKey)).toEqual([
      'Prediction.ShortfallPct',
      'Composition.WaterCementRatio',
      'Batch.ExpectedFinalVsWcmPct',
    ]);
  });

  it('a batch without curing readings has no estimate but keeps its batch warnings', async () => {
    const api = setUp(true);

    const strength = await firstValueFrom(api.getStrength('VM-2026-0142'));

    expect(strength).toMatchObject({
      status: 'InsufficientCuringData',
      atRisk: false,
      projected28DayPsi: null,
      curvePoints: [],
      estimates: [],
    });
    expect(strength.earlyWarnings.map((w) => w.limitKey)).toEqual(['Composition.WaterCementRatio']);
  });

  it('estimates a batch once a cylinder has readings a day apart', async () => {
    const api = setUp(true);
    const reading = {
      internalTempF: 72,
      ambientTempF: 70,
      moisturePct: 93,
      relativeHumidityPct: 95,
      resistivityOhmPerFt: 15,
      co2Ppm: 420,
    };

    await firstValueFrom(
      api.createCuringReading(14201, { ...reading, takenAt: '2026-09-29T12:00:00.000Z' }),
    );
    await firstValueFrom(
      api.createCuringReading(14201, { ...reading, takenAt: '2026-09-30T12:00:00.000Z' }),
    );
    const strength = await firstValueFrom(api.getStrength('VM-2026-0142'));

    expect(strength.status).toBe('Estimated');
    expect(strength.estimates.map((e) => e.adjustmentStage)).toEqual(['None', 'None', 'None']);
    expect(strength.unadjustedCurvePoints).toEqual([]);
  });

  it('answers the strength of an unknown batch with a 404', async () => {
    const api = setUp(true);

    const error = await firstValueFrom(api.getStrength('VM-NOPE')).catch((e: unknown) => e);

    expect((error as HttpErrorResponse).status).toBe(404);
  });

  it('lists batches with early warnings, at-risk first, and follows the Synthetic data switch', async () => {
    const api = setUp(true);

    const all = await firstValueFrom(api.getEarlyWarnings(true));
    const real = await firstValueFrom(api.getEarlyWarnings(false));

    expect(all.map((b) => [b.batchNumber, b.atRisk])).toEqual([
      ['VM-SYN-006', true],
      ['VM-SYN-009', false],
      ['VM-2026-0142', false],
    ]);
    expect(real.map((b) => b.batchNumber)).toEqual(['VM-2026-0142']);
  });

  it('answers the health check, so the status chip is green without the server', async () => {
    setUp(true);

    const health = await firstValueFrom(TestBed.inject(HttpClient).get('/api/health'));

    expect(health).toEqual({ status: 'ok' });
  });

  it('talks to the real server when useMocks is false', () => {
    const api = setUp(false);

    api.getBatches().subscribe();

    TestBed.inject(HttpTestingController).expectOne('/api/batches');
  });
});
