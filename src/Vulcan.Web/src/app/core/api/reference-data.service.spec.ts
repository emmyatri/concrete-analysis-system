import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MOCK_SPECIFIC_GRAVITIES, MOCK_VALIDATION_LIMITS } from './mock/mock-data';
import { ReferenceDataService } from './reference-data.service';

describe('ReferenceDataService', () => {
  let service: ReferenceDataService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ReferenceDataService);
    http = TestBed.inject(HttpTestingController);
  });

  const answer = () => {
    http.expectOne('/api/validation-limits').flush(MOCK_VALIDATION_LIMITS);
    http.expectOne('/api/specific-gravities').flush(MOCK_SPECIFIC_GRAVITIES);
  };

  it('loads the limits and gravities once, however many times it is asked', () => {
    service.load();
    service.load();
    answer();
    service.load();

    http.verify();
    expect(service.status()).toBe('ready');
  });

  it('gives each key its Reject and Warn rows', () => {
    service.load();
    answer();

    const ph = service.limitFor('Batch.MixWaterPh');
    expect(ph?.reject).toMatchObject({ min: 1, max: 9 });
    expect(ph?.warn).toMatchObject({ min: 7, max: 7.5 });
    expect(service.limitFor('Composition.CementPct')?.warn).toBeUndefined();
    expect(service.limitFor('No.SuchKey')).toBeUndefined();
  });

  it('shares the specific gravities by material', () => {
    service.load();
    answer();

    expect(service.specificGravities().get('FlyAsh')).toBe(2.35);
  });

  it('reports a failure, and can load again', () => {
    service.load();
    http.expectOne('/api/validation-limits').flush(null, { status: 0, statusText: 'Unknown Error' });
    http.match('/api/specific-gravities');
    expect(service.status()).toBe('failed');

    service.reload();
    answer();
    expect(service.status()).toBe('ready');
  });
});
