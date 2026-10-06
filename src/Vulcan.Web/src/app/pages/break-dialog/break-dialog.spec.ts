import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { providePrimeNG } from 'primeng/config';
import { firstValueFrom } from 'rxjs';

import {
  clearSavedMockBatches,
  mockApiInterceptor,
} from '../../core/api/mock/mock-api.interceptor';
import { PendingBreak } from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { USE_MOCKS } from '../../core/api/use-mocks';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { BreakDialog, BreakSaved, ageInDays } from './break-dialog';

// jsdom has no ResizeObserver, and PrimeNG's dialog may use one.
globalThis.ResizeObserver ??= class {
  observe = vi.fn();
  unobserve = vi.fn();
  disconnect = vi.fn();
};

describe('BreakDialog', () => {
  let fixture: ComponentFixture<BreakDialog>;
  let saved: BreakSaved[];
  let stale: number;

  /** VM-2026-0142's first cylinder, A1, cast 09/29/26. */
  const A1: PendingBreak = {
    specimenId: 14201,
    batchNumber: 'VM-2026-0142',
    label: 'A1',
    testAgeDays: 'Day3',
    castAt: '2026-09-29T11:40:00Z',
    isSynthetic: false,
  };

  const open = async (cylinder = A1) => {
    clearSavedMockBatches();
    TestBed.configureTestingModule({
      imports: [BreakDialog],
      providers: [
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
        { provide: USE_MOCKS, useValue: true },
      ],
    });
    TestBed.inject(ReferenceDataService).load();
    fixture = TestBed.createComponent(BreakDialog);
    fixture.componentRef.setInput('cylinder', cylinder);
    fixture.componentRef.setInput('visible', true);
    saved = [];
    stale = 0;
    fixture.componentInstance.saved.subscribe((result) => saved.push(result));
    fixture.componentInstance.stale.subscribe(() => stale++);
    await settle();
  };

  const settle = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };
  // The dialog is appended to the page body, so look there, not inside the fixture.
  const query = (testId: string) => document.body.querySelector(`[data-testid="${testId}"]`);
  const text = (testId: string) => query(testId)?.textContent?.replace(/\s+/g, ' ').trim();
  const form = () => (fixture.componentInstance as unknown as { form: BreakDialog['form'] }).form;
  const strength = () => form().controls.strengthPsi;
  const brokenAt = () => form().controls.brokenAt;
  const saveDisabled = () => (query('save')?.querySelector('button') as HTMLButtonElement).disabled;
  const enter = async (psi: string) => {
    strength().setValue(psi);
    strength().markAsTouched();
    await settle();
  };
  const save = async () => {
    await settle();
    (query('save')?.querySelector('button') as HTMLButtonElement).click();
    await settle();
  };
  const breakOf = async (batchNumber: string, label: string) =>
    (await firstValueFrom(TestBed.inject(VulcanApiService).getBatch(batchNumber))).specimens.find(
      (s) => s.label === label,
    )?.breakResult;

  afterEach(() => fixture?.destroy());

  it('shows the batch, cylinder, cast date and age, and starts the break date at today', async () => {
    await open();

    const values = [...document.body.querySelectorAll('[data-testid="cylinder"] dd')].map((dd) =>
      dd.textContent?.trim(),
    );
    expect(values).toEqual([
      'VM-2026-0142',
      'A1 · 3-day',
      '09/29/26',
      `${ageInDays(A1.castAt)} days`,
    ]);
    expect(brokenAt().value?.toDateString()).toBe(new Date().toDateString());
    const today = new Date();
    const shownDate = [today.getMonth() + 1, today.getDate(), today.getFullYear()]
      .map((n) => String(n).padStart(2, '0'))
      .join('/');
    expect(document.body.querySelector<HTMLInputElement>('#break-date')!.value).toBe(shownDate);
    expect(saveDisabled()).toBe(true);
  });

  it.each(['0', '10001', '10,001'])('rejects %s PSI and blocks Save', async (psi) => {
    await open();
    await enter(psi);

    expect(strength().hasError('limit')).toBe(true);
    expect(saveDisabled()).toBe(true);
  });

  it('rejects 4500.5: whole numbers only', async () => {
    await open();
    await enter('4500.5');

    expect(strength().errors?.['decimals']?.message).toBe('Whole numbers only');
    expect(saveDisabled()).toBe(true);
  });

  it.each([
    ['1', 1],
    ['10000', 10000],
    ['10,000', 10000],
    ['4500', 4500],
  ])('saves %s PSI', async (psi, expected) => {
    await open();
    await enter(psi);
    await save();

    expect(saved).toEqual([expect.objectContaining({ label: 'A1', strengthPsi: expected })]);
    expect(fixture.componentInstance.visible()).toBe(false);
    expect((await breakOf('VM-2026-0142', 'A1'))?.strengthPsi).toBe(expected);
  });

  it('saves 2,999 PSI in amber, with a warning', async () => {
    await open();
    await enter('2999');

    expect(document.body.querySelector('app-field.app-field--warn')).not.toBeNull();
    expect(saveDisabled()).toBe(false);

    await save();

    expect(saved[0].warnings.map((w) => w.key)).toEqual(['Break.StrengthPsi']);
    const batch = await firstValueFrom(TestBed.inject(VulcanApiService).getBatch('VM-2026-0142'));
    expect(batch.warnings.some((w) => w.entityType === 'BreakResult' && w.value === 2999)).toBe(
      true,
    );
  });

  it('rejects a break date before the cast date', async () => {
    await open();
    brokenAt().setValue(new Date(2026, 8, 28));

    expect(brokenAt().errors?.['breakDate']?.message).toBe("Can't be before the cast date");
  });

  it('allows a break on the cast date itself', async () => {
    await open();
    brokenAt().setValue(new Date(2026, 8, 29, 18, 0));

    expect(brokenAt().valid).toBe(true);
  });

  it('rejects a break date in the future', async () => {
    await open();
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    brokenAt().setValue(tomorrow);

    expect(brokenAt().errors?.['breakDate']?.message).toBe("Can't be in the future");
  });

  it('requires a break date', async () => {
    await open();
    brokenAt().setValue(null);

    expect(brokenAt().hasError('required')).toBe(true);
  });

  it('saves an earlier break date as noon that day', async () => {
    await open();
    await enter('4500');
    brokenAt().setValue(new Date(2026, 9, 2));
    await save();

    const result = await breakOf('VM-2026-0142', 'A1');
    expect(new Date(result!.brokenAt).toString()).toBe(new Date(2026, 9, 2, 12, 0).toString());
  });

  it('says the cylinder is already broken when the server answers 409, and asks for a reload', async () => {
    await open();
    await firstValueFrom(
      TestBed.inject(VulcanApiService).createBreakResult(A1.specimenId, {
        brokenAt: new Date().toISOString(),
        strengthPsi: 5000,
      }),
    );
    await enter('4500');
    await save();

    expect(text('save-banner')).toBe('This cylinder already has a break result');
    expect(stale).toBe(1);
    expect(saved).toEqual([]);
    expect(saveDisabled()).toBe(true);
    expect(fixture.componentInstance.visible()).toBe(true);
  });
});
