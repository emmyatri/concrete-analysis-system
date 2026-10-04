import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { providePrimeNG } from 'primeng/config';

import { MOCK_BATCH_DETAILS } from '../../core/api/mock/mock-batch-details';
import { MOCK_SPECIFIC_GRAVITIES, MOCK_VALIDATION_LIMITS } from '../../core/api/mock/mock-data';
import { mockApiInterceptor } from '../../core/api/mock/mock-api.interceptor';
import { BatchDetail } from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { USE_MOCKS } from '../../core/api/use-mocks';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { BatchDetailPage } from './batch-detail';

// jsdom has no ResizeObserver, and PrimeNG's tab list uses one to size its scroll buttons.
globalThis.ResizeObserver ??= class {
  observe = vi.fn();
  unobserve = vi.fn();
  disconnect = vi.fn();
};

describe('BatchDetailPage', () => {
  let root: HTMLElement;
  let harness: RouterTestingHarness;

  /** useMocks true: answered by the fake data. False: answered by hand with HttpTestingController. */
  const open = async (batchNumber: string, useMocks = true) => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [{ path: 'batches/:batchNumber', component: BatchDetailPage }],
          withComponentInputBinding(),
        ),
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        provideHttpClientTesting(),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
        { provide: USE_MOCKS, useValue: useMocks },
      ],
    });
    if (useMocks) TestBed.inject(ReferenceDataService).load();
    harness = await RouterTestingHarness.create(`/batches/${batchNumber}`);
    root = harness.routeNativeElement as HTMLElement;
  };

  const text = (selector: string) =>
    root.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const rows = (testId: string) => [...root.querySelectorAll(`[data-testid="${testId}"] tbody tr`)];
  /** "Label value" for each row of a label/value list. */
  const pairs = (testId: string) =>
    [...root.querySelectorAll(`[data-testid="${testId}"] > div`)].map((div) =>
      [...div.children].map((el) => el.textContent?.trim()).join(' '),
    );
  const cells = (row: Element) =>
    [...row.querySelectorAll('td')].map((td) => td.textContent?.replace(/\s+/g, ' ').trim());

  it('shows a batch with its cylinders, breaks, readings and warnings', async () => {
    await open('VM-SYN-006');

    expect(text('h1')).toBe('VM-SYN-006');
    expect(text('[data-testid="synthetic-tag"]')).toBe('Synthetic');
    expect(text('[data-testid="warning-count"]')).toBe('2 warnings');

    // Cylinders & breaks: all 9, and the failing Day-3 break is flagged.
    const cylinders = rows('cylinders');
    expect(cylinders).toHaveLength(9);
    expect(cells(cylinders[0])).toEqual(
      expect.arrayContaining(['D3-1', 'Day 3', '11.93', '5.99', '2,950 ⚠', '7', 'View']),
    );

    // Curing readings: 63, grouped under 9 cylinders.
    const readings = rows('readings');
    expect(readings.filter((r) => r.classList.contains('group-row'))).toHaveLength(9);
    expect(readings.filter((r) => !r.classList.contains('group-row'))).toHaveLength(63);
    expect(text('[data-testid="readings"] .group-row')).toBe('Cylinder D3-1 · 7 readings');

    // Warnings: both saved warnings, each saying which record it's on.
    expect(rows('warnings').map((r) => cells(r).slice(0, 3))).toEqual([
      ['Recipe', 'Water / cementitious', '0.603 ⚠'],
      ['Cylinder D3-1 · break', 'Break strength (PSI)', '2,950 ⚠'],
    ]);
  });

  it('highlights only the flagged value in the summary', async () => {
    await open('VM-SYN-006');

    expect(text('[data-testid="ratio-flagged"]')).toBe('0.603 ⚠');
    expect(root.querySelector('[data-testid="ph-flagged"]')).toBeNull();
    expect(text('[data-testid="summary"]')).toContain('6,200 PSI');
  });

  it('flags the mix water pH on VM-SYN-009', async () => {
    await open('VM-SYN-009');

    expect(text('[data-testid="ph-flagged"]')).toBe('7.6 ⚠');
    expect(text('[data-testid="warning-count"]')).toBe('1 warning');
  });

  it('shows the composition and says the oxides are all within normal range', async () => {
    await open('VM-SYN-006');

    expect(pairs('composition')).toEqual([
      'Cement 10.00',
      'Water 19.00',
      'Coarse agg. 40.00',
      'Fine agg. 25.00',
      'SCM (none) 0.00',
      'Air / other 6.00',
    ]);
    expect(pairs('oxides')).toEqual(['Al₂O₃ 4.90', 'Fe₂O₃ 3.20', 'MgO 2.20', 'SO₃ 2.70']);
    expect(text('[data-testid="chemistry-status"]')).toBe('All within normal range');
  });

  it('flags an oxide outside its normal range', async () => {
    const source = MOCK_BATCH_DETAILS.find((b) => b.batchNumber === 'VM-SYN-001')!;
    const batch: BatchDetail = {
      ...source,
      oxides: { ...source.oxides, magnesiumOxidePct: 4.4 },
      warnings: [
        {
          id: 1,
          entityType: 'CementOxideAnalysis',
          entityId: source.id,
          fieldName: 'MagnesiumOxidePct',
          limitKey: 'Oxide.MagnesiumOxidePct',
          value: 4.4,
          message: 'The magnesium oxide percent is outside the normal range [0.1-4.0]',
          createdAtUtc: source.createdAtUtc,
        },
      ],
    };
    await open('VM-SYN-001', false);
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(ReferenceDataService).load();
    http.expectOne('/api/validation-limits').flush(MOCK_VALIDATION_LIMITS);
    http.expectOne('/api/specific-gravities').flush(MOCK_SPECIFIC_GRAVITIES);
    http.expectOne('/api/batches/VM-SYN-001').flush(batch);
    harness.detectChanges();

    expect(text('[data-testid="chemistry-status"]')).toBe('⚠ MgO 4.40 · normal 0.1–4.0');
    expect(text('[data-testid="oxides"] .flagged')).toBe('4.40 ⚠');
  });

  it('shows cylinders that are not broken yet on a real batch', async () => {
    await open('VM-2026-0142');

    expect(root.querySelector('[data-testid="synthetic-tag"]')).toBeNull();
    expect(rows('cylinders').map((r) => cells(r)[5])).toEqual(Array(6).fill('Not broken'));
    expect(text('[data-testid="audit"]')).toContain('Changed by J. Rivera');
    expect(text('.tabs-card')).toContain('No curing readings yet.');
  });

  it('links each entry button to its screen for this batch', async () => {
    await open('VM-SYN-006');

    const href = (label: string, within = '.page-actions') =>
      [...root.querySelectorAll(`${within} a`)]
        .find((a) => a.textContent?.includes(label))
        ?.getAttribute('href');
    expect(href('Edit batch')).toBe('/batches/VM-SYN-006/edit');
    expect(href('Curing readings')).toBe('/curing?batch=VM-SYN-006');
    expect(href('Add cylinder')).toBe('/batches/VM-SYN-006/cylinders/new');
    expect(href('Enter break')).toBe('/breaks?batch=VM-SYN-006');
    expect(href('View', '[data-testid="cylinders"]')).toBe(
      '/curing?batch=VM-SYN-006&cylinder=D3-1',
    );
  });

  it('shows a friendly message for an unknown batch number', async () => {
    await open('VM-NOPE-999');

    expect(text('[data-testid="not-found"] h1')).toBe('No batch VM-NOPE-999');
    expect(root.querySelector('[data-testid="summary"]')).toBeNull();
  });

  it('offers Try again when the local service fails', async () => {
    await open('VM-SYN-006', false);
    const http = TestBed.inject(HttpTestingController);
    http
      .expectOne('/api/batches/VM-SYN-006')
      .flush(null, { status: 0, statusText: 'Unknown Error' });
    harness.detectChanges();

    expect(text('[data-testid="failed"] h1')).toBe("Couldn't open VM-SYN-006");

    (root.querySelector('[data-testid="failed"] button') as HTMLButtonElement).click();
    harness.detectChanges();
    http
      .expectOne('/api/batches/VM-SYN-006')
      .flush(MOCK_BATCH_DETAILS.find((b) => b.batchNumber === 'VM-SYN-006')!);
    harness.detectChanges();

    expect(text('h1')).toBe('VM-SYN-006');
  });
});
