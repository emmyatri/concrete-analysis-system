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

/** Avoid rendering Chart.js canvases in jsdom. */
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

describe('BatchReport', () => {
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

  it('renders engineering recommendations for an at-risk batch', async () => {
    const root = await render('VM-SYN-006');

    const recommendations = root.querySelector('[data-testid="report-recommendations"]');

    expect(recommendations).not.toBeNull();

    expect(text(root, '[data-testid="report-recommendations"]')).toContain(
      'Review projected compressive strength',
    );

    expect(text(root, '[data-testid="report-recommendations"]')).toContain(
      'Review water-to-cementitious ratio',
    );
  });

  it('shows the strength risk and recorded validation warnings', async () => {
    const root = await render('VM-SYN-006');

    expect(text(root, '[data-testid="report-at-risk"]')).toContain(
      'VM-SYN-006 is at risk of missing its target',
    );

    expect(text(root, '[data-testid="report-validation-warnings"]')).toContain(
      'Water / cementitious',
    );
  });

  it('does not duplicate recommendations for the same warning code', async () => {
    const root = await render('VM-SYN-006');

    const titles = Array.from(root.querySelectorAll('.report-recommendation strong')).map(
      (element) => element.textContent?.trim(),
    );

    expect(titles.filter((title) => title === 'Review water-to-cementitious ratio')).toHaveLength(
      1,
    );
  });

  it('shows the empty recommendation state for an on-track batch', async () => {
    const root = await render('VM-SYN-001');

    expect(root.querySelector('[data-testid="report-no-recommendations"]')).not.toBeNull();

    expect(root.querySelector('[data-testid="report-recommendations"]')).toBeNull();

    expect(root.querySelector('[data-testid="report-no-strength-warnings"]')).not.toBeNull();
  });

  it('displays a not-found message for an unknown batch', async () => {
    const root = await render('VM-NOPE');

    expect(text(root, '.report-message')).toContain('Batch not found');

    expect(root.querySelector('[data-testid="report-document"]')).toBeNull();
  });
});
