import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';

import { mockApiInterceptor } from '../../core/api/mock/mock-api.interceptor';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { USE_MOCKS } from '../../core/api/use-mocks';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { BatchEntry } from './batch-entry';
import { createBatchForm, toCreateBatchRequest } from './batch-form';

describe('BatchEntry', () => {
  let fixture: ComponentFixture<BatchEntry>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BatchEntry],
      providers: [
        provideRouter([]),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
        // Specific gravities come from the fake data, the same as `npm start`.
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        { provide: USE_MOCKS, useValue: true },
      ],
    });
    TestBed.inject(ReferenceDataService).load();
    fixture = TestBed.createComponent(BatchEntry);
    fixture.detectChanges();
  });

  // The form is protected in the template; tests reach it the same way the template does.
  const form = () => (fixture.componentInstance as unknown as { form: BatchEntry['form'] }).form;
  const el = () => fixture.nativeElement as HTMLElement;
  const tile = (id: string) => el().querySelector(`[data-testid="${id}"] .tile-value`)?.textContent?.trim();

  it('shows the Batch and Composition cards', () => {
    const titles = Array.from(el().querySelectorAll('.card-header h2')).map((h) => h.textContent?.trim());
    expect(titles).toEqual(['Batch', 'Composition']);
  });

  it('calculates the totals live, matching the hi-fi mockup', () => {
    form().controls.composition.patchValue({
      cementPct: '16',
      waterPct: '17',
      coarseAggregatePct: '40',
      fineAggregatePct: '25',
      scmType: 'FlyAsh',
      scmPct: '2',
    });
    fixture.detectChanges();

    expect(tile('total')).toBe('100.00%');
    expect(tile('air-other')).toBe('0.00%');
    expect(tile('water-cement')).toBe('0.31');
  });

  it('requires an SCM type when SCM % is more than 0, and None when it is 0', () => {
    const { scmType, scmPct } = form().controls.composition.controls;

    scmPct.setValue('2');
    expect(scmType.errors?.['scmType']?.message).toBe('Choose the SCM type when SCM is more than 0%');

    scmType.setValue('Slag');
    expect(scmType.valid).toBe(true);

    scmPct.setValue('0');
    expect(scmType.errors?.['scmType']?.message).toBe('Must be None when SCM is 0%');
  });

  it('rejects batch numbers with spaces or symbols', () => {
    const batchNumber = form().controls.batch.controls.batchNumber;

    batchNumber.setValue('VM 2026/01');
    expect(batchNumber.errors?.['batchNumber']?.message).toBe('Letters, digits and dashes only');

    batchNumber.setValue('VM-2026-0142');
    expect(batchNumber.valid).toBe(true);
  });
});

describe('toCreateBatchRequest', () => {
  it('trims the batch number and turns typed text into numbers', () => {
    const form = createBatchForm(TestBed.inject(NonNullableFormBuilder));
    form.patchValue({
      batch: { batchNumber: ' VM-2026-0142 ', finalWeightLb: '4,380.00', expectedFinalBreakPsi: '6000', cementType: 'TypeIII', mixWaterPh: '7.2' },
      composition: { cementPct: '12', waterPct: '17', coarseAggregatePct: '40', fineAggregatePct: '25', scmType: 'FlyAsh', scmPct: '2' },
    });

    const request = toCreateBatchRequest(form);

    expect(request.batch).toMatchObject({ batchNumber: 'VM-2026-0142', finalWeightLb: 4380, expectedFinalBreakPsi: 6000, cementType: 'TypeIII', mixWaterPh: 7.2 });
    expect(request.composition).toMatchObject({ cementPct: 12, scmType: 'FlyAsh', scmPct: 2 });
  });
});
