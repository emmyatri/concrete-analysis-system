import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { providePrimeNG } from 'primeng/config';

import {
  clearSavedMockBatches,
  mockApiInterceptor,
} from '../../core/api/mock/mock-api.interceptor';
import { USE_MOCKS } from '../../core/api/use-mocks';
import { SyntheticDataSetting } from '../../core/session/synthetic-data';
import { TECHNICIAN_SETTINGS_URL, TechnicianSession } from '../../core/session/technician-session';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { PlaceholderPage } from '../placeholder-page';
import { HomePage } from './home';

describe('HomePage', () => {
  let root: HTMLElement;
  let harness: RouterTestingHarness;

  /** useMocks true: answered by the fake data. False: answered by hand with HttpTestingController. */
  const open = async (useMocks = true) => {
    clearSavedMockBatches();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: '', component: HomePage },
            {
              path: 'batches/:batchNumber',
              component: PlaceholderPage,
              data: { heading: 'Batch detail' },
            },
          ],
          withComponentInputBinding(),
        ),
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        provideHttpClientTesting(),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
        { provide: USE_MOCKS, useValue: useMocks },
      ],
    });
    TestBed.inject(TechnicianSession).start('J. Rivera').subscribe();
    TestBed.inject(HttpTestingController).expectOne(TECHNICIAN_SETTINGS_URL).flush(null);
    harness = await RouterTestingHarness.create('/');
    root = harness.routeNativeElement as HTMLElement;
  };

  const text = (selector: string) =>
    root.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const batchNumbers = () =>
    [...root.querySelectorAll('[data-testid="recent"] tbody .batch-number')].map((el) =>
      el.textContent?.trim(),
    );

  it('greets the technician by name', async () => {
    await open();

    expect(text('[data-testid="greeting"]')).toMatch(
      /^Good (morning|afternoon|evening), J\. Rivera$/,
    );
  });

  it('links the three quick actions', async () => {
    await open();
    const href = (testId: string) =>
      root.querySelector(`[data-testid="${testId}"]`)?.getAttribute('href');

    expect(href('action-new')).toBe('/batches/new');
    expect(href('action-find')).toBe('/batches');
    expect(href('action-breaks')).toBe('/breaks');
  });

  it('lists the 8 newest batches, synthetic ones marked', async () => {
    await open();

    expect(batchNumbers()).toEqual([
      'VM-2026-0142',
      'VM-2026-0141',
      'VM-2026-0139',
      'VM-SYN-009',
      'VM-SYN-008',
      'VM-SYN-007',
      'VM-SYN-006',
      'VM-SYN-005',
    ]);
    expect(root.querySelectorAll('[data-testid="recent"] .badge-synthetic').length).toBe(5);
  });

  it('hides synthetic batches when the switch is off', async () => {
    await open();
    TestBed.inject(SyntheticDataSetting).show.set(false);
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(batchNumbers()).toEqual(['VM-2026-0142', 'VM-2026-0141', 'VM-2026-0139']);
  });

  it('opens Batch Detail when a row is clicked', async () => {
    await open();
    root.querySelector<HTMLElement>('[data-testid="recent"] tbody tr')!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/batches/VM-2026-0142');
  });

  it('says so when there are no batches yet', async () => {
    await open(false);
    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/batches')
      .flush([]);
    harness.detectChanges();

    expect(text('[data-testid="recent-message"]')).toBe(
      'No batches yet. Use New batch to enter the first one.',
    );
  });

  it('shows the error when the list cannot load', async () => {
    await open(false);
    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/batches')
      .flush(null, { status: 0, statusText: 'Unknown Error' });
    harness.detectChanges();

    expect(text('[data-testid="recent-message"]')).not.toBe('');
    expect(root.querySelector('[data-testid="recent"]')).toBeNull();
  });
});
