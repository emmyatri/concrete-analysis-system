import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { USE_MOCKS } from '../use-mocks';
import { VulcanApiService } from '../vulcan-api.service';
import { mockApiInterceptor } from './mock-api.interceptor';

describe('mockApiInterceptor', () => {
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

    expect(batches.slice(0, 3).map((b) => b.batchNumber)).toEqual(['VM-2026-0142', 'VM-2026-0141', 'VM-2026-0139']);
  });

  it('finds batches by any part of the number, ignoring upper/lowercase', async () => {
    const api = setUp(true);

    const lower = await firstValueFrom(api.getBatches({ search: 'vm-syn' }));
    const upper = await firstValueFrom(api.getBatches({ search: 'VM-SYN' }));

    expect(lower.map((b) => b.batchNumber)).toEqual(['VM-SYN-0003', 'VM-SYN-0002', 'VM-SYN-0001']);
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

    expect(limits.find((l) => l.key === 'Composition.CementPct')).toMatchObject({ severity: 'Reject', min: 10, max: 15 });
    expect(gravities.find((g) => g.material === 'Cement')?.value).toBe(3.15);
  });

  it('rejects a duplicate batch number with a 400 the screens can show', async () => {
    setUp(true);

    const error = await firstValueFrom(
      TestBed.inject(HttpClient).post('/api/batches', { batch: { batchNumber: 'vm-2026-0142' } }),
    ).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(HttpErrorResponse);
    expect((error as HttpErrorResponse).error.errors['Batch.BatchNumber']).toEqual(['Batch number vm-2026-0142 already exists']);
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

  it('talks to the real server when useMocks is false', () => {
    const api = setUp(false);

    api.getBatches().subscribe();

    TestBed.inject(HttpTestingController).expectOne('/api/batches');
  });
});
