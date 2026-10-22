import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { Component, input } from '@angular/core';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { providePrimeNG } from 'primeng/config';

import { MOCK_BATCH_DETAILS } from '../../core/api/mock/mock-batch-details';
import { MOCK_SPECIFIC_GRAVITIES, MOCK_VALIDATION_LIMITS } from '../../core/api/mock/mock-data';
import {
  clearSavedMockBatches,
  mockApiInterceptor,
} from '../../core/api/mock/mock-api.interceptor';
import { BatchDetail } from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { USE_MOCKS } from '../../core/api/use-mocks';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { StrengthChart } from '../strength-chart/strength-chart';
import { BatchDetailPage } from './batch-detail';

/** Stands in for p-chart, since jsdom can't draw on a canvas. */
// eslint-disable-next-line @angular-eslint/component-selector -- must match PrimeNG's selector
@Component({ selector: 'p-chart', template: '<canvas [attr.aria-label]="ariaLabel()"></canvas>' })
class FakeChart {
  readonly type = input<string>();
  readonly data = input<unknown>();
  readonly options = input<unknown>();
  readonly plugins = input<unknown>();
  readonly height = input<string>();
  readonly ariaLabel = input<string>();
}

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
    clearSavedMockBatches();
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
    TestBed.overrideComponent(StrengthChart, { set: { imports: [FakeChart] } });
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
      expect.arrayContaining(['D3-1', 'Day 3', '11.93 in', '5.99 in', '2,950 PSI ⚠', '7', 'View']),
    );
    // Broken at reads "06/24 @ 12:05".
    expect(cells(cylinders[0])[4]).toMatch(/^\d\d\/\d\d @ \d\d:\d\d$/);

    // Curing readings: 63, grouped under 9 cylinders.
    const readings = rows('readings');
    expect(readings.filter((r) => r.classList.contains('group-row'))).toHaveLength(9);
    expect(readings.filter((r) => !r.classList.contains('group-row'))).toHaveLength(63);
    expect(text('[data-testid="readings"] .group-row')).toBe('Cylinder D3-1 · 7 readings');
    // Each value shows its unit.
    expect(cells(readings[1]).slice(1)).toEqual([
      '78.4 °F',
      '69.5 °F',
      '91.6%',
      '96.1%',
      '15.98 Ω/ft',
      '416.8 ppm',
    ]);

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

  /* Test for a batch with a weight outside its normal range; confirms that the
  flagged value is displayed and styled correctly. */
  it('flags a weight outside its normal range', async () => {
    const source = MOCK_BATCH_DETAILS.find((b) => b.batchNumber === 'VM-SYN-001')!;
    const batch: BatchDetail = {
      ...source,
      finalWeightLb: 35,
      warnings: [
        {
          id: 1,
          entityType: 'Batch',
          entityId: source.id,
          fieldName: 'FinalWeightLb',
          limitKey: 'Batch.FinalWeightLb',
          value: 35,
          message: 'Sample weight is outside the normal range [20-30] for a 12x6 cylinder',
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

    expect(text('[data-testid="weight-flagged"]')).toBe('35 lb ⚠');
    expect(text('[data-testid="warning-count"]')).toBe('1 warning');
    expect(
      root.querySelector('[data-testid="weight-flagged"]')?.classList.contains('flagged'),
    ).toBe(true);
  });

  /* Test for a batch with a normal weight; confirms that the flagged value is not displayed. */
  it('does not flag a weight within its normal range', async () => {
    const source = MOCK_BATCH_DETAILS.find((b) => b.batchNumber === 'VM-SYN-001')!;
    const batch: BatchDetail = {
      ...source,
      finalWeightLb: 25,
      warnings: [],
    };
    await open('VM-SYN-001', false);
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(ReferenceDataService).load();
    http.expectOne('/api/validation-limits').flush(MOCK_VALIDATION_LIMITS);
    http.expectOne('/api/specific-gravities').flush(MOCK_SPECIFIC_GRAVITIES);
    http.expectOne('/api/batches/VM-SYN-001').flush(batch);
    harness.detectChanges();

    expect(root.querySelector('[data-testid="weight-flagged"]')).toBeNull();
  });

  /** Opens VM-SYN-001 with the given changes, answered by hand like the real server. */
  const openWith = async (changes: Partial<BatchDetail>) => {
    const source = MOCK_BATCH_DETAILS.find((b) => b.batchNumber === 'VM-SYN-001')!;
    await open('VM-SYN-001', false);
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(ReferenceDataService).load();
    http.expectOne('/api/validation-limits').flush(MOCK_VALIDATION_LIMITS);
    http.expectOne('/api/specific-gravities').flush(MOCK_SPECIFIC_GRAVITIES);
    http.expectOne('/api/batches/VM-SYN-001').flush({ ...source, ...changes });
    harness.detectChanges();
    return source;
  };

  it('shows both pH values in one cell', async () => {
    await open('VM-SYN-001');

    expect(text('[data-testid="ph"]')).toBe('7.2 / 7.4');
    expect(root.querySelector('[data-testid="surface-ph-flagged"]')).toBeNull();
  });

  it('flags a cement surface pH outside its normal range', async () => {
    const source = MOCK_BATCH_DETAILS.find((b) => b.batchNumber === 'VM-SYN-001')!;
    await openWith({
      cementSurfacePh: 8.5,
      warnings: [
        {
          id: 1,
          entityType: 'Batch',
          entityId: source.id,
          fieldName: 'CementSurfacePh',
          limitKey: 'Batch.CementSurfacePh',
          value: 8.5,
          message: 'Cement surface pH is outside the normal range [7.1-8.0]',
          createdAtUtc: source.createdAtUtc,
        },
      ],
    });

    expect(text('[data-testid="ph"]')).toBe('7.2 / 8.5 ⚠');
    expect(root.querySelector('[data-testid="ph-flagged"]')).toBeNull();
    expect(rows('warnings').map((r) => cells(r).slice(0, 3))).toEqual([
      ['Batch', 'Cement surface pH', '8.5 ⚠'],
    ]);
  });

  it('shows a dash for an old batch with no surface pH', async () => {
    await openWith({ cementSurfacePh: null, warnings: [] });

    expect(text('[data-testid="ph"]')).toBe('7.2 / —');
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
    expect(rows('cylinders').map((r) => cells(r)[5])).toEqual(Array(6).fill('Record break'));
    expect(root.querySelector('[data-testid="record-break"]')?.getAttribute('href')).toBe(
      '/breaks?batch=VM-2026-0142&cylinder=A1',
    );
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
    expect(href('Enter break')).toBe('/breaks?batch=VM-SYN-006');
    expect(href('View', '[data-testid="cylinders"]')).toBe(
      '/curing?batch=VM-SYN-006&cylinder=D3-1',
    );
  });

  it('Add cylinder opens the popup, and a saved cylinder shows in the table once it closes', async () => {
    await open('VM-2026-0142');
    const dialog = () => document.body.querySelector('.p-dialog');
    expect(dialog()).toBeNull();

    (root.querySelector('[data-testid="add-cylinder"]') as HTMLButtonElement).click();
    harness.detectChanges();
    await harness.fixture.whenStable();
    expect(dialog()?.textContent).toContain('Add cylinder · VM-2026-0142');

    const form = harness.routeDebugElement!.query((el) => el.name === 'app-add-cylinder-dialog')
      .componentInstance as { form: { patchValue(v: object): void } };
    form.form.patchValue({ label: 'B1', lengthIn: '12', widthIn: '6', testAgeDays: 'Day28' });
    harness.detectChanges();
    await harness.fixture.whenStable();
    (document.body.querySelector('[data-testid="save"] button') as HTMLButtonElement).click();
    for (let i = 0; i < 3; i++) {
      harness.detectChanges();
      await harness.fixture.whenStable();
    }
    expect(document.body.querySelector('.p-dialog')).toBeNull();

    expect(rows('cylinders').map((r) => cells(r)[0])).toContain('B1');
    expect(text('h1')).toBe('VM-2026-0142');
  });

  it('opens Add cylinder straight away from ?addCylinder=1', async () => {
    await open('VM-2026-0142');
    await harness.navigateByUrl('/batches/VM-2026-0142?addCylinder=1');
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(document.body.querySelector('.p-dialog')?.textContent).toContain(
      'Add cylinder · VM-2026-0142',
    );
  });

  it('shows a friendly message for an unknown batch number', async () => {
    await open('VM-NOPE-999');

    expect(text('[data-testid="not-found"] h1')).toBe('No batch VM-NOPE-999');
    expect(root.querySelector('[data-testid="summary"]')).toBeNull();
  });

  it('shows the strength chart for the batch', async () => {
    await open('VM-SYN-006');

    expect(root.querySelector('app-strength-chart canvas')?.getAttribute('aria-label')).toContain(
      'VM-SYN-006 strength over time. Predicted 28-day strength 4,449 PSI.',
    );
  });

  it('still shows the batch when the strength estimate fails', async () => {
    await open('VM-SYN-006', false);
    const http = TestBed.inject(HttpTestingController);
    http
      .expectOne('/api/batches/VM-SYN-006')
      .flush(MOCK_BATCH_DETAILS.find((b) => b.batchNumber === 'VM-SYN-006')!);
    http
      .expectOne('/api/batches/VM-SYN-006/strength')
      .flush(null, { status: 500, statusText: 'Server Error' });
    harness.detectChanges();

    expect(text('h1')).toBe('VM-SYN-006');
    expect(text('[data-testid="chart-failed"]')).toContain("Couldn't load the strength estimate.");
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
