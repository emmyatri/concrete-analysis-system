import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { Router, provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';
import { throwError } from 'rxjs';

import {
  clearSavedMockBatches,
  mockApiInterceptor,
} from '../../core/api/mock/mock-api.interceptor';
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
  const tile = (id: string) =>
    el().querySelector(`[data-testid="${id}"] .tile-value`)?.textContent?.trim();

  it('shows the Batch, Cement oxides and Composition cards', () => {
    const titles = Array.from(el().querySelectorAll('.card-header h2')).map((h) =>
      h.textContent?.trim(),
    );
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
    expect(scmType.errors?.['scmType']?.message).toBe(
      'Choose the SCM type when SCM is more than 0%',
    );

    scmType.setValue('Slag');
    expect(scmType.valid).toBe(true);

    scmPct.setValue('0');
    expect(scmType.errors?.['scmType']?.message).toBe('Must be None when SCM is 0%');
  });

  it('never counts the cement oxides in the composition Total', () => {
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

    form().controls.oxides.patchValue({
      aluminaPct: '7.5',
      ironOxidePct: '6',
      magnesiumOxidePct: '6',
      sulfurTrioxidePct: '4.5',
    });
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
      batch: {
        batchNumber: 'VM-2026-0142',
        castAt: new Date(2026, 8, 29, 11, 40),
        finalWeightLb: '25',
        expectedFinalBreakPsi: '6000',
        cementType: 'TypeIII',
        mixWaterPh: '7.2',
        cementSurfacePh: '7.5',
      },
      composition: {
        cementPct: '12',
        waterPct: '17',
        coarseAggregatePct: '44',
        fineAggregatePct: '25',
        scmType: 'FlyAsh',
        scmPct: '2',
      },
      oxides: {
        aluminaPct: '4.8',
        ironOxidePct: '2.8',
        magnesiumOxidePct: '1.8',
        sulfurTrioxidePct: '2.2',
      },
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
      batch: {
        batchNumber: 'VSN-003',
        castAt: new Date(2026, 8, 29, 11, 40),
        finalWeightLb: '25',
        expectedFinalBreakPsi: '3000',
        cementType: 'TypeII',
        mixWaterPh: '7.2',
        cementSurfacePh: '7.5',
      },
      // 14 + 17 + 40 + 29 + 2 = 102%
      composition: {
        cementPct: '14',
        waterPct: '17',
        coarseAggregatePct: '40',
        fineAggregatePct: '29',
        scmType: 'FlyAsh',
        scmPct: '2',
      },
      oxides: {
        aluminaPct: '4.8',
        ironOxidePct: '2.8',
        magnesiumOxidePct: '1.8',
        sulfurTrioxidePct: '2.2',
      },
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
    finalWeightLb.setValue('25.00');
    expect(finalWeightLb.valid).toBe(true);
  });

  it('takes whole numbers for expected break and at most 2 decimals for weight', () => {
    const { expectedFinalBreakPsi, finalWeightLb } = form().controls.batch.controls;

    expectedFinalBreakPsi.setValue('6000.5');
    expect(expectedFinalBreakPsi.errors?.['decimals']?.message).toBe('Whole numbers only');
    expectedFinalBreakPsi.setValue('6000');
    expect(expectedFinalBreakPsi.valid).toBe(true);

    finalWeightLb.setValue('25.123');
    expect(finalWeightLb.errors?.['decimals']?.message).toBe('Max 2 decimal places');
    finalWeightLb.setValue('25.12');
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

  const saveButton = () =>
    el().querySelector('[data-testid="save-batch"] button') as HTMLButtonElement;
  const weightField = () => el().querySelector('#finalWeightLb')?.closest('app-field');

  it('labels sample weight and shows the typical and allowed range', () => {
    const field = weightField();
    expect(field?.querySelector('label')?.textContent?.trim()).toBe('Sample weight (lb)');
    expect(field?.querySelector('.app-field-hint')?.textContent?.trim()).toBe(
      '20–30 lb typical (12×6 cylinder) · 10–40 allowed',
    );
  });

  it('does not warn on 25 lb sample weight and keeps Save enabled', async () => {
    fillValidBatch('VM-WEIGHT-0025');
    const { finalWeightLb } = form().controls.batch.controls;
    finalWeightLb.setValue('25');
    finalWeightLb.markAsTouched();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(weightField()?.classList).not.toContain('app-field--warn');
    expect(weightField()?.classList).not.toContain('app-field--error');
    expect(saveButton().disabled).toBe(false);
  });

  it('warns in amber on 35 lb sample weight and still allows Save', async () => {
    fillValidBatch('VM-WEIGHT-0035');
    const { finalWeightLb } = form().controls.batch.controls;
    finalWeightLb.setValue('35');
    finalWeightLb.markAsTouched();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(weightField()?.classList).toContain('app-field--warn');
    expect(saveButton().disabled).toBe(false);
  });

  it('blocks Save on 45 lb sample weight with a red error', async () => {
    fillValidBatch('VM-WEIGHT-0045');
    const { finalWeightLb } = form().controls.batch.controls;
    finalWeightLb.setValue('45');
    finalWeightLb.markAsTouched();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(weightField()?.classList).toContain('app-field--error');
    expect(saveButton().disabled).toBe(true);
  });

  it('warns just outside 20–30 lb and rejects just outside 10–40 lb', async () => {
    fillValidBatch('VM-WEIGHT-EDGES');
    const { finalWeightLb } = form().controls.batch.controls;
    const setWeight = async (value: string) => {
      finalWeightLb.setValue(value);
      finalWeightLb.markAsTouched();
      fixture.detectChanges();
      await fixture.whenStable();
    };

    await setWeight('9.99');
    expect(weightField()?.classList).toContain('app-field--error');
    expect(saveButton().disabled).toBe(true);

    await setWeight('10');
    expect(weightField()?.classList).toContain('app-field--warn');
    expect(saveButton().disabled).toBe(false);

    await setWeight('20');
    expect(weightField()?.classList).not.toContain('app-field--warn');
    expect(weightField()?.classList).not.toContain('app-field--error');

    await setWeight('30');
    expect(weightField()?.classList).not.toContain('app-field--warn');
    expect(weightField()?.classList).not.toContain('app-field--error');

    await setWeight('30.01');
    expect(weightField()?.classList).toContain('app-field--warn');
    expect(saveButton().disabled).toBe(false);

    await setWeight('40');
    expect(weightField()?.classList).toContain('app-field--warn');
    expect(saveButton().disabled).toBe(false);

    await setWeight('40.01');
    expect(weightField()?.classList).toContain('app-field--error');
    expect(saveButton().disabled).toBe(true);
  });

  const surfacePhField = () => el().querySelector('#cementSurfacePh')?.closest('app-field');
  const setSurfacePh = async (value: string) => {
    const { cementSurfacePh } = form().controls.batch.controls;
    cementSurfacePh.setValue(value);
    cementSurfacePh.markAsTouched();
    fixture.detectChanges();
    await fixture.whenStable();
  };

  it('puts Cement surface pH right after Mix water pH, with its normal and allowed range', () => {
    const labels = Array.from(
      el().querySelectorAll('section[aria-labelledby="batch-card-title"] app-field label'),
    ).map((label) => label.textContent?.trim());
    expect(labels.slice(-2)).toEqual(['Mix water pH', 'Cement surface pH']);
    expect(surfacePhField()?.querySelector('.app-field-hint')?.textContent?.trim()).toBe(
      '7.1–8 normal · 0–14 allowed',
    );
  });

  it('accepts 7.5, warns on 8.5 and blocks 15 for Cement surface pH', async () => {
    fillValidBatch('VM-PH-RANGE');

    await setSurfacePh('7.5');
    expect(surfacePhField()?.classList).not.toContain('app-field--warn');
    expect(surfacePhField()?.classList).not.toContain('app-field--error');
    expect(saveButton().disabled).toBe(false);

    await setSurfacePh('8.5');
    expect(surfacePhField()?.classList).toContain('app-field--warn');
    expect(saveButton().disabled).toBe(false);

    await setSurfacePh('15');
    expect(surfacePhField()?.classList).toContain('app-field--error');
    expect(saveButton().disabled).toBe(true);
  });

  it('blocks Save when Cement surface pH is empty', async () => {
    fillValidBatch('VM-PH-EMPTY');

    await setSurfacePh('');
    expect(form().controls.batch.controls.cementSurfacePh.errors?.['required']).toBe(true);
    expect(saveButton().disabled).toBe(true);
  });

  it('warns just outside 7.1–8.0 and rejects just outside 0–14 for Cement surface pH', async () => {
    fillValidBatch('VM-PH-EDGES');

    await setSurfacePh('-0.01');
    expect(surfacePhField()?.classList).toContain('app-field--error');
    expect(saveButton().disabled).toBe(true);

    await setSurfacePh('0');
    expect(surfacePhField()?.classList).toContain('app-field--warn');
    expect(saveButton().disabled).toBe(false);

    await setSurfacePh('7.09');
    expect(surfacePhField()?.classList).toContain('app-field--warn');

    await setSurfacePh('7.1');
    expect(surfacePhField()?.classList).not.toContain('app-field--warn');
    expect(surfacePhField()?.classList).not.toContain('app-field--error');

    await setSurfacePh('8');
    expect(surfacePhField()?.classList).not.toContain('app-field--warn');
    expect(surfacePhField()?.classList).not.toContain('app-field--error');

    await setSurfacePh('8.01');
    expect(surfacePhField()?.classList).toContain('app-field--warn');
    expect(saveButton().disabled).toBe(false);

    await setSurfacePh('14');
    expect(surfacePhField()?.classList).toContain('app-field--warn');
    expect(saveButton().disabled).toBe(false);

    await setSurfacePh('14.01');
    expect(surfacePhField()?.classList).toContain('app-field--error');
    expect(saveButton().disabled).toBe(true);
  });

  // ---------- Saving ----------

  /** A batch that passes every check, with a batch number that isn't taken yet. */
  const fillValidBatch = (batchNumber: string) =>
    form().patchValue({
      batch: {
        batchNumber,
        castAt: new Date(2026, 9, 5, 14, 0),
        finalWeightLb: '25',
        expectedFinalBreakPsi: '6000',
        cementType: 'TypeI',
        mixWaterPh: '7.2',
        cementSurfacePh: '7.5',
      },
      composition: {
        cementPct: '10',
        waterPct: '17',
        coarseAggregatePct: '45',
        fineAggregatePct: '26',
        scmType: 'FlyAsh',
        scmPct: '2',
      },
      oxides: {
        aluminaPct: '4.8',
        ironOxidePct: '2.8',
        magnesiumOxidePct: '1.8',
        sulfurTrioxidePct: '2.2',
      },
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

    expect(navigate).toHaveBeenCalledWith(['/batches', 'VM-SAVE-0001'], { queryParams: {} });
  });

  it('Save & add cylinders opens the new batch with Add cylinder open', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fillValidBatch('VM-SAVE-0002');

    await click('save-add-cylinders');

    expect(navigate).toHaveBeenCalledWith(['/batches', 'VM-SAVE-0002'], {
      queryParams: { addCylinder: '1' },
    });
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

    expect(el().querySelector('[data-testid="save-banner"]')?.textContent).toContain(
      "Can't reach the local service",
    );
    expect(form().controls.batch.controls.batchNumber.value).toBe('VM-SAVE-0004');
  });
});

describe('BatchEntry as Edit batch', () => {
  let fixture: ComponentFixture<BatchEntry>;

  const open = async (batchNumber: string) => {
    clearSavedMockBatches();
    TestBed.configureTestingModule({
      imports: [BatchEntry],
      providers: [
        provideRouter([]),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        { provide: USE_MOCKS, useValue: true },
      ],
    });
    TestBed.inject(ReferenceDataService).load();
    fixture = TestBed.createComponent(BatchEntry);
    fixture.componentRef.setInput('batchNumber', batchNumber);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const form = () => (fixture.componentInstance as unknown as { form: BatchEntry['form'] }).form;
  const el = () => fixture.nativeElement as HTMLElement;
  const click = async (testId: string) => {
    fixture.detectChanges();
    await fixture.whenStable();
    (el().querySelector(`[data-testid="${testId}"] button`) as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  it('fills the form from the saved batch, with number and cast date locked', async () => {
    await open('VM-SYN-006');

    expect(el().querySelector('h1')?.textContent?.trim()).toBe('Edit batch VM-SYN-006');
    const { batch, composition } = form().controls;
    expect(batch.controls.batchNumber.value).toBe('VM-SYN-006');
    expect(batch.controls.batchNumber.disabled).toBe(true);
    expect(batch.controls.castAt.disabled).toBe(true);
    expect(batch.controls.expectedFinalBreakPsi.value).toBe('6200');
    expect(composition.controls.waterPct.value).toBe('19');
    expect(el().querySelector('[data-testid="save-add-cylinders"]')).toBeNull();
    expect(el().querySelector('[data-testid="save-batch"]')?.textContent).toContain('Save changes');
  });

  it('saves the changes and goes back to the batch', async () => {
    await open('VM-SYN-006');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    form().controls.batch.controls.mixWaterPh.setValue('7.3');

    await click('save-batch');

    expect(navigate).toHaveBeenCalledWith(['/batches', 'VM-SYN-006']);
    const saved = await new Promise<{ mixWaterPh: number; isSynthetic: boolean }>((resolve) =>
      TestBed.inject(VulcanApiService).getBatch('VM-SYN-006').subscribe(resolve),
    );
    expect(saved.mixWaterPh).toBe(7.3);
    expect(saved.isSynthetic).toBe(true);
  });

  it('sends the cast date exactly as it was loaded', async () => {
    await open('VM-SYN-006');
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const update = vi.spyOn(TestBed.inject(VulcanApiService), 'updateBatch');
    const loadedCastAt = await new Promise<string>((resolve) =>
      TestBed.inject(VulcanApiService)
        .getBatch('VM-SYN-006')
        .subscribe((batch) => resolve(batch.castAt)),
    );

    await click('save-batch');

    expect(update.mock.calls[0][1].castAt).toBe(loadedCastAt);
  });

  it('says so when the batch does not exist', async () => {
    await open('VM-NOPE');

    expect(el().querySelector('[data-testid="save-banner"]')?.textContent).toContain(
      'Batch VM-NOPE was not found.',
    );
    expect(el().querySelector('.columns')).toBeNull();
  });
});

describe('toCreateBatchRequest', () => {
  it('trims the batch number and turns typed text into numbers', () => {
    const form = createBatchForm(TestBed.inject(NonNullableFormBuilder));
    form.patchValue({
      batch: {
        batchNumber: ' VM-2026-0142 ',
        finalWeightLb: '25.00',
        expectedFinalBreakPsi: '6000',
        cementType: 'TypeIII',
        mixWaterPh: '7.2',
        cementSurfacePh: '7.5',
      },
      composition: {
        cementPct: '12',
        waterPct: '17',
        coarseAggregatePct: '40',
        fineAggregatePct: '25',
        scmType: 'FlyAsh',
        scmPct: '2',
      },
      oxides: {
        aluminaPct: '4.8',
        ironOxidePct: '2.8',
        magnesiumOxidePct: '1.8',
        sulfurTrioxidePct: '2.2',
      },
    });

    const request = toCreateBatchRequest(form);

    expect(request.batch).toMatchObject({
      batchNumber: 'VM-2026-0142',
      finalWeightLb: 25,
      expectedFinalBreakPsi: 6000,
      cementType: 'TypeIII',
      mixWaterPh: 7.2,
      cementSurfacePh: 7.5,
    });
    expect(request.composition).toMatchObject({ cementPct: 12, scmType: 'FlyAsh', scmPct: 2 });
    expect(request.oxides.aluminaPct).toBe(4.8);
  });
});
