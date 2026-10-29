import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
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
import { ageInDays } from '../break-dialog/break-dialog';
import { BreakResultsPage } from './break-results';

// jsdom has no ResizeObserver, and PrimeNG's dialog may use one.
globalThis.ResizeObserver ??= class {
  observe = vi.fn();
  unobserve = vi.fn();
  disconnect = vi.fn();
};

describe('BreakResultsPage', () => {
  let root: HTMLElement;
  let harness: RouterTestingHarness;

  /** useMocks true: answered by the fake data. False: answered by hand with HttpTestingController. */
  const open = async (url = '/breaks', useMocks = true) => {
    clearSavedMockBatches();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [{ path: 'breaks', component: BreakResultsPage }],
          withComponentInputBinding(),
        ),
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        provideHttpClientTesting(),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
        { provide: USE_MOCKS, useValue: useMocks },
      ],
    });
    if (useMocks) TestBed.inject(ReferenceDataService).load();
    harness = await RouterTestingHarness.create(url);
    root = harness.routeNativeElement as HTMLElement;
    await settle();
  };

  const settle = async () => {
    await harness.fixture.whenStable();
    harness.detectChanges();
  };
  const text = (selector: string, within: ParentNode = root) =>
    within.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const rows = () => [...root.querySelectorAll('[data-testid="pending"] tbody tr')];
  const cells = (row: Element) =>
    [...row.querySelectorAll('td')].map((td) => td.textContent?.replace(/\s+/g, ' ').trim());
  const labels = () => rows().map((row) => cells(row)[1]);
  const filter = async (value: string) => {
    const input = root.querySelector<HTMLInputElement>('[data-testid="filter"]')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    await settle();
  };
  /** The dialog is appended to the page body. */
  const dialog = () => document.body.querySelector('[data-testid="break-dialog"]');
  const recordBreak = async (psi: string) => {
    const input = document.body.querySelector<HTMLInputElement>('#break-strength')!;
    input.value = psi;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    await settle();
    document.body.querySelector<HTMLButtonElement>('[data-testid="save"] button')!.click();
    await settle();
  };

  afterEach(() => harness?.fixture.destroy());

  it('lists the cylinders waiting to be broken, soonest due first', async () => {
    await open();
    const age = ageInDays('2026-09-29T11:40:00Z');

    expect(labels()).toEqual(['A1', 'A2', 'A3', 'A4', 'A5', 'A6']);
    expect(cells(rows()[0]).slice(0, 5)).toEqual([
      'VM-2026-0142',
      'A1',
      '3 days',
      '09/29/26',
      `${age} days Due`,
    ]);
    // A 28-day cylinder is due only from day 28.
    expect(cells(rows()[5])[4]).toBe(age >= 28 ? `${age} days Due` : `${age} days`);
  });

  it('filters by any part of the batch number, ignoring upper/lowercase', async () => {
    await open();

    await filter('vm-2026');
    expect(rows().length).toBe(6);

    await filter('xyz');
    expect(text('[data-testid="pending-message"]')).toBe(
      "No cylinders waiting on batches matching 'xyz'",
    );
  });

  it('fills in the filter from ?batch=', async () => {
    await open('/breaks?batch=VM-2026-0142');

    expect(root.querySelector<HTMLInputElement>('[data-testid="filter"]')!.value).toBe(
      'VM-2026-0142',
    );
    expect(rows().length).toBe(6);
  });

  it('records a break: the cylinder leaves the list and the result is on the batch', async () => {
    await open();
    (rows()[0].querySelector('[data-testid="record-break"] button') as HTMLButtonElement).click();
    await settle();

    expect(text('[data-testid="cylinder"]', document.body)).toContain('A1');

    await recordBreak('4500');

    expect(labels()).toEqual(['A2', 'A3', 'A4', 'A5', 'A6']);
    expect(text('[data-testid="saved"]')).toContain('Saved VM-2026-0142 · A1: 4,500 PSI.');
    const batch = await firstValueFrom(TestBed.inject(VulcanApiService).getBatch('VM-2026-0142'));
    expect(batch.specimens.find((s) => s.label === 'A1')?.breakResult?.strengthPsi).toBe(4500);
  });

  it('shows the warning after saving a 28-day break below 3,000 PSI', async () => {
    await open();
    // A5 is a 28-day cylinder; the 3,000 PSI warning doesn't apply to 3- and 7-day breaks.
    (rows()[4].querySelector('[data-testid="record-break"] button') as HTMLButtonElement).click();
    await settle();
    await recordBreak('2999');

    expect(text('[data-testid="saved"]')).toContain('Break strength is below 3,000 PSI');
    expect(root.querySelector('.saved--warn')).not.toBeNull();
  });

  it('opens the dialog for one cylinder from Batch Detail (?batch=&cylinder=)', async () => {
    await open('/breaks?batch=VM-2026-0142&cylinder=a3');

    expect(dialog()).not.toBeNull();
    expect(text('[data-testid="cylinder"]', document.body)).toContain('A3 · 7-day');
  });

  it('says so when nothing is waiting', async () => {
    await open('/breaks', false);
    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/breaks/pending')
      .flush([]);
    await settle();

    expect(text('[data-testid="pending-message"]')).toBe('No cylinders are waiting to be broken.');
  });
});
