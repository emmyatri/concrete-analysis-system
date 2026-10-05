import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { Router, provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';
import { throwError } from 'rxjs';

import { clearSavedMockBatches, mockApiInterceptor } from '../../core/api/mock/mock-api.interceptor';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { USE_MOCKS } from '../../core/api/use-mocks';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { BatchEntry } from './batch-entry';
import { createBatchForm, toCreateBatchRequest } from './batch-form';

describe('BatchEntry', () => {
  let fixture: ComponentFixture<BatchEntry>;

  beforeEach(() => {
    clearSavedMockBatches();
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

  it('shows the Batch, Cement oxides and Composition cards', () => {
    const titles = Array.from(el().querySelectorAll('.card-header h2')).map((h) => h.textContent?.trim());
    expect(titles).toEqual(['Batch', 'Cement oxides', 'Composition']);
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

  it('never counts the cement oxides in the composition Total', () => {
    form().controls.composition.patchValue({ cementPct: '16', waterPct: '17', coarseAggregatePct: '40', fineAggregatePct: '25', scmType: 'FlyAsh', scmPct: '2' });
    fixture.detectChanges();
    expect(tile('total')).toBe('100.00%');

    form().controls.oxides.patchValue({ aluminaPct: '7.5', ironOxidePct: '6', magnesiumOxidePct: '6', sulfurTrioxidePct: '4.5' });
    fixture.detectChanges();

    expect(tile('total')).toBe('100.00%');
    expect(tile('air-other')).toBe('0.00%');
  });

  it('rejects batch numbers with spaces or symbols', () => {
    const batchNumber = form().controls.batch.controls.batchNumber;

    batchNumber.setValue('VM 2026/01');
    expect(batchNumber.errors?.['batchNumber']?.message).toBe('Letters, digits and dashes only');

    batchNumber.setValue('VM-2026-0142');
    expect(batchNumber.valid).toBe(true);
  });

  it('keeps Save disabled until the form is valid, and shows what blocks it', async () => {
    const save = () => el().querySelector('[data-testid="save-batch"] button') as HTMLButtonElement;
    expect(save().disabled).toBe(true);

    form().patchValue({
      batch: { batchNumber: 'VM-2026-0142', castAt: new Date(2026, 8, 29, 11, 40), finalWeightLb: '4380', expectedFinalBreakPsi: '6000', cementType: 'TypeIII', mixWaterPh: '7.2' },
      composition: { cementPct: '12', waterPct: '17', coarseAggregatePct: '44', fineAggregatePct: '25', scmType: 'FlyAsh', scmPct: '2' },
      oxides: { aluminaPct: '4.8', ironOxidePct: '2.8', magnesiumOxidePct: '1.8', sulfurTrioxidePct: '2.2' },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    expect(save().disabled).toBe(false);

    // 2,000 PSI is outside 3,000–10,000: red, and Save is blocked. pH 7.8 is only a warning.
    const { expectedFinalBreakPsi } = form().controls.batch.controls;
    expectedFinalBreakPsi.setValue('2000');
    expectedFinalBreakPsi.markAsTouched();
    const { mixWaterPh } = form().controls.batch.controls;
    mixWaterPh.setValue('7.8');
    mixWaterPh.markAsTouched();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(save().disabled).toBe(true);
    expect(el().querySelector('[data-testid="save-summary"]')?.textContent?.trim()).toBe(
      '1 error blocks saving · 1 warning',
    );
  });

  it('blocks Save when the total is over 100%, and warns on the water / cementitious ratio', async () => {
    const save = () => el().querySelector('[data-testid="save-batch"] button') as HTMLButtonElement;
    form().patchValue({
      batch: { batchNumber: 'VSN-003', castAt: new Date(2026, 8, 29, 11, 40), finalWeightLb: '10000', expectedFinalBreakPsi: '3000', cementType: 'TypeII', mixWaterPh: '7.2' },
      // 14 + 17 + 40 + 29 + 2 = 102%
      composition: { cementPct: '14', waterPct: '17', coarseAggregatePct: '40', fineAggregatePct: '29', scmType: 'FlyAsh', scmPct: '2' },
      oxides: { aluminaPct: '4.8', ironOxidePct: '2.8', magnesiumOxidePct: '1.8', sulfurTrioxidePct: '2.2' },
    });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(form().valid).toBe(true);
    expect(save().disabled).toBe(true);
    expect(el().querySelector('[data-testid="total"]')?.classList).toContain('tile--error');
    expect(el().querySelector('[data-testid="total-message"]')?.textContent?.trim()).toBe(
      'Total composition must not exceed 100%',
    );
    expect(el().querySelector('[data-testid="water-cement"]')?.classList).toContain('tile--warn');
    expect(el().querySelector('[data-testid="save-summary"]')?.textContent?.trim()).toBe(
      '1 error blocks saving · 1 warning',
    );

    // Back to 100%: Save enables. The ratio warning alone doesn't block it.
    form().controls.composition.controls.fineAggregatePct.setValue('27');
    fixture.detectChanges();
    await fixture.whenStable();
    expect(save().disabled).toBe(false);
  });

  it('treats text and blanks as missing numbers, not valid ones', () => {
    const { finalWeightLb, mixWaterPh } = form().controls.batch.controls;

    finalWeightLb.setValue('abc');
    expect(finalWeightLb.errors?.['number']?.message).toBe('Enter a number');
    finalWeightLb.setValue('1e3');
    expect(finalWeightLb.errors?.['number']?.message).toBe('Enter a number');
    mixWaterPh.setValue('   ');
    expect(mixWaterPh.errors?.['required']).toBe(true);
    finalWeightLb.setValue('4,380.00');
    expect(finalWeightLb.valid).toBe(true);
  });

  it('takes whole numbers for expected break and at most 2 decimals for weight', () => {
    const { expectedFinalBreakPsi, finalWeightLb } = form().controls.batch.controls;

    expectedFinalBreakPsi.setValue('6000.5');
    expect(expectedFinalBreakPsi.errors?.['decimals']?.message).toBe('Whole numbers only');
    expectedFinalBreakPsi.setValue('6000');
    expect(expectedFinalBreakPsi.valid).toBe(true);

    finalWeightLb.setValue('4380.123');
    expect(finalWeightLb.errors?.['decimals']?.message).toBe('Max 2 decimal places');
    finalWeightLb.setValue('4380.12');
    expect(finalWeightLb.valid).toBe(true);
  });

  it('says why Save is off when nothing is red yet, and shows the SCM type error right away', async () => {
    const message = () => el().querySelector('[data-testid="save-summary"]')?.textContent?.trim();
    expect(message()).toBe('Fill in every field to save');

    const { scmType, scmPct } = form().controls.composition.controls;
    scmType.setValue('FlyAsh');
    scmPct.setValue('0');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(scmType.touched).toBe(true);
    expect(message()).toBe('1 error blocks saving');
  });


  // ---------- Saving ----------

  /** A batch that passes every check, with a batch number that isn't taken yet. */
  const fillValidBatch = (batchNumber: string) =>
    form().patchValue({
      batch: { batchNumber, castAt: new Date(2026, 9, 5, 14, 0), finalWeightLb: '4380', expectedFinalBreakPsi: '6000', cementType: 'TypeI', mixWaterPh: '7.2' },
      composition: { cementPct: '10', waterPct: '17', coarseAggregatePct: '45', fineAggregatePct: '26', scmType: 'FlyAsh', scmPct: '2' },
      oxides: { aluminaPct: '4.8', ironOxidePct: '2.8', magnesiumOxidePct: '1.8', sulfurTrioxidePct: '2.2' },
    });
  const click = async (testId: string) => {
    fixture.detectChanges();
    await fixture.whenStable();
    (el().querySelector(`[data-testid="${testId}"] button`) as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  it('saves the batch and opens it', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fillValidBatch('VM-SAVE-0001');

    await click('save-batch');

    expect(navigate).toHaveBeenCalledWith(['/batches', 'VM-SAVE-0001']);
  });

  it('Save & add cylinders opens Add cylinder for the new batch', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fillValidBatch('VM-SAVE-0002');

    await click('save-add-cylinders');

    expect(navigate).toHaveBeenCalledWith(['/batches', 'VM-SAVE-0002', 'cylinders', 'new']);
  });

  it('shows a duplicate batch number from the server next to the Batch number field', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fillValidBatch('VM-2026-0142');

    await click('save-batch');

    const batchNumber = form().controls.batch.controls.batchNumber;
    expect(navigate).not.toHaveBeenCalled();
    expect(batchNumber.errors?.['server']).toBe('Batch number VM-2026-0142 already exists');
    expect(el().querySelector('[data-testid="save-banner"]')).toBeNull();

    // Changing the value clears the server's message.
    batchNumber.setValue('VM-SAVE-0003');
    expect(batchNumber.errors).toBeNull();
  });

  it("shows a banner when the local service can't be reached, and keeps the form", async () => {
    vi.spyOn(TestBed.inject(VulcanApiService), 'createBatch').mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 0 })),
    );
    fillValidBatch('VM-SAVE-0004');

    await click('save-batch');

    expect(el().querySelector('[data-testid="save-banner"]')?.textContent).toContain("Can't reach the local service");
    expect(form().controls.batch.controls.batchNumber.value).toBe('VM-SAVE-0004');
  });

});

describe('toCreateBatchRequest', () => {
  it('trims the batch number and turns typed text into numbers', () => {
    const form = createBatchForm(TestBed.inject(NonNullableFormBuilder));
    form.patchValue({
      batch: { batchNumber: ' VM-2026-0142 ', finalWeightLb: '4,380.00', expectedFinalBreakPsi: '6000', cementType: 'TypeIII', mixWaterPh: '7.2' },
      composition: { cementPct: '12', waterPct: '17', coarseAggregatePct: '40', fineAggregatePct: '25', scmType: 'FlyAsh', scmPct: '2' },
      oxides: { aluminaPct: '4.8', ironOxidePct: '2.8', magnesiumOxidePct: '1.8', sulfurTrioxidePct: '2.2' },
    });

    const request = toCreateBatchRequest(form);

    expect(request.batch).toMatchObject({ batchNumber: 'VM-2026-0142', finalWeightLb: 4380, expectedFinalBreakPsi: 6000, cementType: 'TypeIII', mixWaterPh: 7.2 });
    expect(request.composition).toMatchObject({ cementPct: 12, scmType: 'FlyAsh', scmPct: 2 });
    expect(request.oxides.aluminaPct).toBe(4.8);
  });
});
