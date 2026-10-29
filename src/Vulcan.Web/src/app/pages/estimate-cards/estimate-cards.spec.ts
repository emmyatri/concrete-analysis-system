import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MOCK_BATCH_DETAILS } from '../../core/api/mock/mock-batch-details';
import { mockStrength } from '../../core/api/mock/mock-strength';
import { BatchDetail } from '../../core/api/models';
import { StrengthLoad } from '../strength-chart/strength-chart';
import { EstimateCards } from './estimate-cards';

describe('EstimateCards', () => {
  let fixture: ComponentFixture<EstimateCards>;

  const batchOf = (batchNumber: string) =>
    MOCK_BATCH_DETAILS.find((b) => b.batchNumber === batchNumber)!;
  const render = (batch: BatchDetail, state?: StrengthLoad) => {
    fixture = TestBed.createComponent(EstimateCards);
    fixture.componentRef.setInput('batch', batch);
    fixture.componentRef.setInput(
      'state',
      state ?? { status: 'loaded', strength: mockStrength(batch) },
    );
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };
  const text = (root: HTMLElement, selector: string) =>
    root.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim();

  it('shows each estimate with the adjustment used and its calibration version', () => {
    const root = render(batchOf('VM-SYN-006'));

    expect(text(root, '[data-testid="estimate-7"] [data-testid="estimate-psi"]')).toBe('3,969 PSI');
    expect(text(root, '[data-testid="estimate-7"] [data-testid="basis"]')).toBe(
      'Adjusted using the 3- and 7-day breaks · was 5,048 before breaks · Calibration v1',
    );
    expect(text(root, '[data-testid="estimate-7"] details p')).toContain('ACI 209R-92 (TypeIII)');
  });

  it('shows the actual break next to the estimate with the difference', () => {
    const root = render(batchOf('VM-SYN-006'));

    expect(text(root, '[data-testid="estimate-28"] [data-testid="actual"]')).toBe(
      'Actual break: 4,994 PSI (average of 3) · estimate −5.2% vs actual',
    );
    expect(text(root, '[data-testid="estimate-3"] [data-testid="actual"]')).toBe(
      'Actual break: 3,129 PSI (average of 3) · estimate 0.0% vs actual',
    );
  });

  it('turns the 28-day card amber when the batch is at risk', () => {
    const root = render(batchOf('VM-SYN-006'));

    expect(root.querySelector('[data-testid="estimate-28"]')?.classList).toContain('at-risk');
    expect(text(root, '[data-testid="risk-badge"]')).toBe('⚠ 24% below target');
    expect(root.querySelectorAll('.at-risk')).toHaveLength(1);
  });

  it('leaves an on-track batch neutral', () => {
    const root = render(batchOf('VM-SYN-001'));

    expect(root.querySelector('.at-risk')).toBeNull();
    expect(root.querySelector('[data-testid="risk-badge"]')).toBeNull();
  });

  it('says when each break is due until it is recorded', () => {
    const source = batchOf('VM-SYN-006');
    const batch = {
      ...source,
      specimens: source.specimens.map((s) => ({ ...s, breakResult: null })),
    };
    const strength = { ...mockStrength(batch), atRisk: false };

    const root = render(batch, { status: 'loaded', strength });

    expect(text(root, '[data-testid="estimate-3"] [data-testid="due"]')).toBe(
      'Breaks due 06/24/26',
    );
    expect(text(root, '[data-testid="estimate-28"] [data-testid="due"]')).toBe(
      'Breaks due 07/19/26 · target 6,200 PSI',
    );
    expect(text(root, '[data-testid="estimate-3"] [data-testid="basis"]')).toBe(
      'Before any breaks · Calibration v1',
    );
  });

  it('says there is not enough curing data on every card', () => {
    const root = render(batchOf('VM-2026-0142'));

    expect(root.querySelectorAll('[data-testid="no-estimate"]')).toHaveLength(3);
    expect(text(root, '[data-testid="no-estimate"]')).toBe('Not enough curing data yet');
  });
});
