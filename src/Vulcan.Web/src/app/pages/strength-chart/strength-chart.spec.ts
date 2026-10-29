import { Component, input } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MOCK_BATCH_DETAILS } from '../../core/api/mock/mock-batch-details';
import { mockStrength } from '../../core/api/mock/mock-strength';
import { BatchStrength } from '../../core/api/models';
import { StrengthChart, StrengthLoad } from './strength-chart';

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

describe('StrengthChart', () => {
  let fixture: ComponentFixture<StrengthChart>;

  const render = (state: StrengthLoad, castAt = '2026-06-21T12:00:00Z') => {
    TestBed.overrideComponent(StrengthChart, { set: { imports: [FakeChart] } });
    fixture = TestBed.createComponent(StrengthChart);
    fixture.componentRef.setInput('state', state);
    fixture.componentRef.setInput('castAt', castAt);
    fixture.componentRef.setInput('minimumPsi', 3000);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };
  const strengthOf = (batchNumber: string) =>
    mockStrength(MOCK_BATCH_DETAILS.find((b) => b.batchNumber === batchNumber)!);
  const text = (root: HTMLElement, selector: string) =>
    root.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim();

  it('describes the chart for screen readers', () => {
    const root = render({ status: 'loaded', strength: strengthOf('VM-SYN-006') });

    expect(root.querySelector('canvas')?.getAttribute('aria-label')).toBe(
      'VM-SYN-006 strength over time. Predicted 28-day strength 4,870 PSI. ' +
        'Expected final 6,200 PSI. 9 breaks recorded.',
    );
    expect(root.querySelector('[data-testid="not-enough-data"]')).toBeNull();
    expect(root.querySelector('[data-testid="no-breaks"]')).toBeNull();
  });

  it('says when there is not enough curing data yet, with the latest reading', () => {
    const strength: BatchStrength = {
      ...strengthOf('VM-2026-0142'),
      curingPoints: [
        {
          ...strengthOf('VM-SYN-006').curingPoints[0],
          time: '2026-09-30T05:40:00Z',
        },
      ],
    };

    const root = render({ status: 'loaded', strength }, '2026-09-29T11:40:00Z');

    expect(text(root, '[data-testid="not-enough-data"]')).toBe(
      'Not enough curing data yet Estimates start once a cylinder has a reading at least 24 hours ' +
        'after cast. Latest reading: 18 h.',
    );
  });

  it('says when there are no readings at all', () => {
    const root = render({ status: 'loaded', strength: strengthOf('VM-2026-0142') });

    expect(text(root, '[data-testid="not-enough-data"]')).toContain('No readings yet.');
  });

  it('says when no breaks are recorded yet', () => {
    const root = render({
      status: 'loaded',
      strength: { ...strengthOf('VM-SYN-006'), measuredBreaks: [] },
    });

    expect(text(root, '[data-testid="no-breaks"]')).toBe('No breaks recorded yet');
  });

  it('shows loading, then a failure without a chart', () => {
    const root = render({ status: 'loading' });
    expect(root.querySelector('[data-testid="chart-loading"]')).not.toBeNull();

    fixture.componentRef.setInput('state', {
      status: 'failed',
      message: 'The local API is not responding.',
    });
    fixture.detectChanges();

    expect(text(root, '[data-testid="chart-failed"]')).toBe(
      "Couldn't load the strength estimate. The local API is not responding.",
    );
    expect(root.querySelector('canvas')).toBeNull();
  });
});
