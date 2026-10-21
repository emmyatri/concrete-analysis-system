import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { providePrimeNG } from 'primeng/config';
import { firstValueFrom } from 'rxjs';

import {
  clearSavedMockBatches,
  mockApiInterceptor,
} from '../../core/api/mock/mock-api.interceptor';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { USE_MOCKS } from '../../core/api/use-mocks';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { AddCylinderDialog } from './add-cylinder-dialog';

// jsdom has no ResizeObserver, and PrimeNG's dialog may use one.
globalThis.ResizeObserver ??= class {
  observe = vi.fn();
  unobserve = vi.fn();
  disconnect = vi.fn();
};

describe('AddCylinderDialog', () => {
  let fixture: ComponentFixture<AddCylinderDialog>;
  let saved: string[];

  const open = async (batchNumber: string) => {
    clearSavedMockBatches();
    TestBed.configureTestingModule({
      imports: [AddCylinderDialog],
      providers: [
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
        { provide: USE_MOCKS, useValue: true },
      ],
    });
    TestBed.inject(ReferenceDataService).load();
    fixture = TestBed.createComponent(AddCylinderDialog);
    fixture.componentRef.setInput('batchNumber', batchNumber);
    fixture.componentRef.setInput('visible', true);
    saved = [];
    fixture.componentInstance.saved.subscribe((label) => saved.push(label));
    fixture.detectChanges();
    await fixture.whenStable();
  };

  // The dialog is appended to the page body, so look there, not inside the fixture.
  const query = (testId: string) => document.body.querySelector(`[data-testid="${testId}"]`);
  const text = (testId: string) => query(testId)?.textContent?.replace(/\s+/g, ' ').trim();
  const form = () =>
    (fixture.componentInstance as unknown as { form: AddCylinderDialog['form'] }).form;
  const fill = (
    label: string,
    lengthIn = '12',
    widthIn = '6',
    testAgeDays: 'Day3' | 'Day7' | 'Day28' = 'Day28',
  ) => form().patchValue({ label, lengthIn, widthIn, testAgeDays });
  const click = async (testId: string) => {
    fixture.detectChanges();
    await fixture.whenStable();
    (query(testId)?.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();
  };
  const cylinders = async (batchNumber: string) =>
    (await firstValueFrom(TestBed.inject(VulcanApiService).getBatch(batchNumber))).specimens.map(
      (s) => s.label,
    );

  afterEach(() => fixture?.destroy());

  it('shows the batch number in the title', async () => {
    await open('VM-2026-0142');
    expect(text('add-cylinder-dialog')).toContain('Add cylinder · VM-2026-0142');
  });

  it('accepts 12.00 in long and 6.00 in wide, and rejects 12.01, 6.01 and 0', async () => {
    await open('VM-2026-0142');
    const { lengthIn, widthIn } = form().controls;

    fill('B1', '12.00', '6.00');
    fixture.detectChanges();
    await fixture.whenStable();
    expect(lengthIn.valid && widthIn.valid).toBe(true);

    for (const [control, value] of [
      [lengthIn, '12.01'],
      [widthIn, '6.01'],
      [lengthIn, '0'],
      [widthIn, '0'],
    ] as const) {
      control.setValue(value);
      fixture.detectChanges();
      await fixture.whenStable();
      expect(control.hasError('limit')).toBe(true);
      control.setValue('5');
    }
  });

  it('rejects 5.125 instead of rounding it', async () => {
    await open('VM-2026-0142');
    form().controls.lengthIn.setValue('5.125');
    expect(form().controls.lengthIn.errors?.['decimals']?.message).toBe('Max 2 decimal places');
  });

  it('saves the cylinder and closes', async () => {
    await open('VM-2026-0142');
    fill('B1');

    await click('save');

    expect(saved).toEqual(['B1']);
    expect(fixture.componentInstance.visible()).toBe(false);
    expect(await cylinders('VM-2026-0142')).toContain('B1');
  });

  it('Save & add another stays open, clears the label and keeps the sizes', async () => {
    await open('VM-2026-0142');
    fill('B1', '12', '6', 'Day7');
    await click('save-another');
    form().controls.label.setValue('B2');
    await click('save-another');

    expect(saved).toEqual(['B1', 'B2']);
    expect(fixture.componentInstance.visible()).toBe(true);
    expect(text('added')).toContain('Added B1, B2');
    expect(form().controls.label.value).toBe('');
    expect(form().getRawValue()).toMatchObject({
      lengthIn: '12',
      widthIn: '6',
      testAgeDays: 'Day7',
    });
  });

  it('rejects a label already used on this batch, but allows it on another batch', async () => {
    await open('VM-2026-0142');
    fill('A1');
    await click('save');

    expect(form().controls.label.errors?.['server']).toBe(
      'Cylinder A1 already exists on this batch',
    );
    expect(fixture.componentInstance.visible()).toBe(true);

    const created = await firstValueFrom(
      TestBed.inject(VulcanApiService).createSpecimen('VM-SYN-001', {
        label: 'A1',
        lengthIn: 12,
        widthIn: 6,
        testAgeDays: 'Day3',
      }),
    );
    expect(created.label).toBe('A1');
  });

  it('says so when the batch does not exist', async () => {
    await open('VM-NOPE-999');
    fill('A1');
    await click('save');

    expect(text('save-banner')).toContain("There's no batch VM-NOPE-999");
  });

  it('Cancel closes without saving', async () => {
    await open('VM-2026-0142');
    fill('B9');
    await click('cancel');

    expect(fixture.componentInstance.visible()).toBe(false);
    expect(saved).toEqual([]);
  });
});
