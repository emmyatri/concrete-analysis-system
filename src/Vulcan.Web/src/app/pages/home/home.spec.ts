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
import { MOCK_BATCHES } from '../../core/api/mock/mock-data';
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
    root.querySelector<HTMLElement>('[data-testid="recent"] tbody tr.row-link')!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/batches/VM-2026-0142');
  });

  const flushWarnings = (body: object[] = []) => {
    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url.startsWith('/api/warnings/early'))
      .flush(body);
  };

  it('lists only at-risk batches with shortfall and the top reason', async () => {
    await open();

    const numbers = [...root.querySelectorAll('[data-testid="at-risk"] tbody .batch-number')].map(
      (el) => el.textContent?.trim(),
    );
    // Largest shortfall first: moisture loss, high w/c, then cold-cured.
    expect(numbers).toEqual(['VM-SYN-003', 'VM-SYN-006', 'VM-SYN-002']);
    const row = text('[data-testid="at-risk"] tbody tr');
    expect(row).toContain('4,535 PSI');
    expect(row).toContain('6,000 PSI');
    expect(row).toContain('−1,465 (24%)');
    expect(text('[data-testid="at-risk"] tbody tr')).toContain('Projected 28-day strength');
  });

  it('says early warnings are not available when the API has no endpoint for them yet', async () => {
    await open(false);
    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/batches')
      .flush([]);
    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url.startsWith('/api/warnings/early'))
      .flush(null, { status: 404, statusText: 'Not Found' });
    harness.detectChanges();

    expect(text('[data-testid="at-risk-message"]')).toBe("Early warnings aren't available yet.");
  });

  it('hides synthetic at-risk batches when the switch is off', async () => {
    await open();
    TestBed.inject(SyntheticDataSetting).show.set(false);
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(text('[data-testid="at-risk-message"]')).toBe('No batches at risk.');
    expect(root.querySelector('[data-testid="at-risk"]')).toBeNull();
  });

  it('opens Batch Detail from an at-risk row', async () => {
    await open();
    root.querySelector<HTMLElement>('[data-testid="at-risk"] tbody tr.row-link')!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/batches/VM-SYN-003');
  });

  it('groups the batches under date headings, newest first', async () => {
    await open(false);
    const batch = (batchNumber: string, castAt: Date) => ({
      ...MOCK_BATCHES[0],
      batchNumber,
      castAt: castAt.toISOString(),
    });
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 1);
    const yesterday = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1, 12, 0);
    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/batches')
      .flush([
        batch('B-3', today),
        batch('B-2', yesterday),
        batch('B-1', new Date(2020, 0, 1)),
        batch('B-0', new Date(2019, 5, 1)),
      ]);
    flushWarnings();
    harness.detectChanges();

    const headings = [...root.querySelectorAll('[data-testid="date-group"]')].map((el) =>
      el.textContent?.trim(),
    );
    expect(headings).toEqual(['Today', 'Yesterday', 'Older']);
    expect(batchNumbers()).toEqual(['B-3', 'B-2', 'B-1', 'B-0']);
  });

  it('says so when there are no batches yet', async () => {
    await open(false);
    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/batches')
      .flush([]);
    flushWarnings();
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
    flushWarnings();
    harness.detectChanges();

    expect(text('[data-testid="recent-message"]')).not.toBe('');
    expect(root.querySelector('[data-testid="recent"]')).toBeNull();
  });
});
