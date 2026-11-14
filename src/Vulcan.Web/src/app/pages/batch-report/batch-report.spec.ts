import { Component, input } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { USE_MOCKS } from '../../core/api/use-mocks';
import {
  clearSavedMockBatches,
  mockApiInterceptor,
} from '../../core/api/mock/mock-api.interceptor';
import { BatchReport } from './batch-report';
import { StrengthChart } from '../strength-chart/strength-chart';

/**
 * Replace the real chart with a lightweight component so the
 * report template can be tested without Chart.js canvas rendering.
 */
@Component({
  selector: 'app-strength-chart',
  template: '<div data-testid="fake-strength-chart"></div>',
})
class FakeStrengthChart {
  readonly state = input<unknown>();
  readonly castAt = input<string>();
  readonly minimumPsi = input<number | null>();
  readonly printMode = input<boolean>();
  readonly height = input<string>();
  readonly fontSize = input<number>();
}

describe('BatchReport — FR-19 acceptance tests', () => {
  let fixture: ComponentFixture<BatchReport>;

  beforeEach(() => {
    clearSavedMockBatches();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: USE_MOCKS, useValue: true },
      ],
    });

    TestBed.overrideComponent(BatchReport, {
      remove: {
        imports: [StrengthChart],
      },
      add: {
        imports: [FakeStrengthChart],
      },
    });
  });

  const render = async (batchNumber: string): Promise<HTMLElement> => {
    fixture = TestBed.createComponent(BatchReport);
    fixture.componentRef.setInput('batchNumber', batchNumber);
    fixture.detectChanges();

    await fixture.whenStable();
    fixture.detectChanges();

    return fixture.nativeElement as HTMLElement;
  };

  const text = (root: HTMLElement, selector: string): string =>
    root.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';

  it('renders all seven report sections for VM-SYN-006', async () => {
    const root = await render('VM-SYN-006');

    const requiredSections = [
      'report-batch-record',
      'report-composition',
      'report-oxides',
      'report-specimens',
      'report-strength-section',
      'report-predictions',
      'report-warnings',
    ];

    expect(root.querySelector('[data-testid="report-document"]')).not.toBeNull();

    for (const section of requiredSections) {
      expect(root.querySelector(`[data-testid="${section}"]`)).not.toBeNull();
    }
  });

  it('renders the batch record and identifying information', async () => {
    const root = await render('VM-SYN-006');

    const header = text(root, '[data-testid="report-header"]');
    const batchRecord = text(root, '[data-testid="report-batch-record"]');

    expect(header).toContain('Concrete Batch Report');
    expect(header).toContain('VM-SYN-006');

    expect(batchRecord).toContain('VM-SYN-006');
    expect(batchRecord).toContain('Sample weight');
    expect(batchRecord).toContain('Water / cementitious ratio');
  });

  it('renders the strength chart component in the report', async () => {
    const root = await render('VM-SYN-006');

    const strengthSection = root.querySelector('[data-testid="report-strength-section"]');

    expect(strengthSection).not.toBeNull();

    expect(strengthSection?.querySelector('[data-testid="fake-strength-chart"]')).not.toBeNull();

    expect(text(root, '[data-testid="report-strength-section"]')).toContain('Strength Development');
  });

  it('renders strength predictions and calibration information', async () => {
    const root = await render('VM-SYN-006');

    const predictions = text(root, '[data-testid="report-predictions"]');

    expect(predictions).toContain('Strength Predictions and Calibration');
    expect(predictions).toContain('Projected 28-day strength');
    expect(predictions).toContain('4,733 PSI');
    expect(predictions).toContain('Calculation Method and Calibration');

    expect(root.querySelector('.predictions-table')).not.toBeNull();
  });

  it('renders strength-risk and recorded validation warnings', async () => {
    const root = await render('VM-SYN-006');

    const warnings = root.querySelector('[data-testid="report-warnings"]');

    expect(warnings).not.toBeNull();

    expect(text(root, '[data-testid="report-at-risk"]')).toContain(
      'VM-SYN-006 is at risk of missing its target',
    );

    expect(text(root, '[data-testid="report-validation-warnings"]')).toContain(
      'Water / cementitious',
    );

    expect(
      root.querySelector('[data-testid="report-validation-warnings"] tbody tr'),
    ).not.toBeNull();
  });

  it('identifies synthetic batches as SYNTHETIC TEST DATA', async () => {
    const root = await render('VM-SYN-006');

    expect(text(root, '[data-testid="report-synthetic"]')).toBe('SYNTHETIC TEST DATA');

    expect(text(root, '[data-testid="report-synthetic-footer"]')).toBe('SYNTHETIC TEST DATA');
  });

  it('shows the required empty-break state for a batch without breaks', async () => {
    const root = await render('VM-2026-0142');

    expect(text(root, '[data-testid="no-breaks"]')).toBe('No breaks recorded yet');

    expect(text(root, '[data-testid="report-specimens"]')).toContain('Breaks recorded 0');
  });

  it('shows no early-strength warnings for an on-track batch', async () => {
    const root = await render('VM-SYN-001');

    expect(text(root, '[data-testid="report-no-strength-warnings"]')).toContain(
      'No early-strength warnings were reported',
    );

    expect(root.querySelector('[data-testid="report-at-risk"]')).toBeNull();
  });

  it('displays a not-found message for an unknown batch', async () => {
    const root = await render('VM-NOPE');

    expect(text(root, '.report-message')).toContain('Batch not found');

    expect(root.querySelector('[data-testid="report-document"]')).toBeNull();
  });

  it('posts the PDF export request with the current batch number', async () => {
    const postMessage = vi.fn();

    vi.stubGlobal('chrome', {
      webview: { postMessage },
    });

    try {
      const root = await render('VM-SYN-006');

      const button = root.querySelector<HTMLButtonElement>('[data-testid="save-report-pdf"]');

      expect(button).not.toBeNull();
      expect(button?.disabled).toBe(false);

      button?.click();

      expect(postMessage).toHaveBeenCalledExactlyOnceWith({
        type: 'save-report-pdf',
        batchNumber: 'VM-SYN-006',
      });
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it('posts the print request when running in WebView2', async () => {
    const postMessage = vi.fn();

    vi.stubGlobal('chrome', {
      webview: { postMessage },
    });

    try {
      const root = await render('VM-SYN-006');

      const button = root.querySelector<HTMLButtonElement>('[data-testid="print-report"]');

      expect(button).not.toBeNull();

      button?.click();

      expect(postMessage).toHaveBeenCalledExactlyOnceWith({
        type: 'print-report',
      });
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it('falls back to window.print when WebView2 is unavailable', async () => {
    vi.stubGlobal('chrome', undefined);

    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);

    try {
      const root = await render('VM-SYN-006');

      const button = root.querySelector<HTMLButtonElement>('[data-testid="print-report"]');

      expect(button).not.toBeNull();

      button?.click();

      expect(print).toHaveBeenCalledOnce();
    } finally {
      print.mockRestore();
      vi.unstubAllGlobals();
    }
  });

  it('hides Save as PDF when WebView2 is unavailable', async () => {
    vi.stubGlobal('chrome', undefined);

    try {
      const root = await render('VM-SYN-006');

      expect(root.querySelector('[data-testid="save-report-pdf"]')).toBeNull();
      expect(root.querySelector('[data-testid="print-report"]')).not.toBeNull();
    } finally {
      vi.unstubAllGlobals();
    }
  });
});
