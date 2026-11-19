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

  const renderMiss = (estimatedPsi: number) => {
    const source = batchOf('VM-SYN-006');
    const batch = {
      ...source,
      specimens: source.specimens.map((s) =>
        s.testAgeDays === 'Day28' && s.breakResult
          ? { ...s, breakResult: { ...s.breakResult, strengthPsi: 10000 } }
          : s,
      ),
    };
    const base = mockStrength(batch);
    const strength = {
      ...base,
      estimates: base.estimates.map((e) => (e.horizonDays === 28 ? { ...e, estimatedPsi } : e)),
    };

    return render(batch, { status: 'loaded', strength });
  };

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
      'Actual break: 4,994 PSI · average of 3 cylinders',
    );
    expect(text(root, '[data-testid="estimate-28"] [data-testid="difference"]')).toBe(
      'Estimate was 261 PSI (5.2%) below the actual break',
    );
    expect(text(root, '[data-testid="estimate-3"] [data-testid="difference"]')).toBe(
      'Estimate matched the actual break',
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

  it('shows the estimate as outside the target and only on the 28-day card', () => {
    const root = render(batchOf('VM-SYN-006'));

    expect(
      root.querySelector('[data-testid="estimate-3"] [data-testid="accuracy-target"]'),
    ).toBeNull();
    expect(
      root.querySelector('[data-testid="estimate-7"] [data-testid="accuracy-target"]'),
    ).toBeNull();
    expect(text(root, '[data-testid="estimate-28"] [data-testid="accuracy-target"]')).toBe(
      'Outside the 5% target',
    );
  });

  it('flips the verdict for accuracy when limit is changed to 6', () => {
    const batch = batchOf('VM-SYN-006');
    const strength = { ...mockStrength(batch), accuracyTargetPct: 6 };
    const root = render(batch, { status: 'loaded', strength });

    expect(text(root, '[data-testid="estimate-28"] [data-testid="accuracy-target"]')).toBe(
      'Within the 6% target',
    );
  });

  it.each([
    { miss: '-4.96%', estimatedPsi: 9504, shown: '5.0', verdict: 'Within' },
    { miss: '5.00%', estimatedPsi: 10500, shown: '5.0', verdict: 'Within' },
    { miss: '5.04%', estimatedPsi: 10504, shown: '5.04', verdict: 'Outside' },
    { miss: '5.004%', estimatedPsi: 10500.4, shown: '5.004', verdict: 'Outside' },
    { miss: '5.0004%', estimatedPsi: 10500.04, shown: '>5.000', verdict: 'Outside' },
  ])(
    'shows a $miss miss as "$shown%" and $verdict the target',
    ({ estimatedPsi, shown, verdict }) => {
      const root = renderMiss(estimatedPsi);

      expect(text(root, '[data-testid="estimate-28"] [data-testid="difference"]')).toContain(
        `(${shown}%)`,
      );
      expect(text(root, '[data-testid="estimate-28"] [data-testid="accuracy-target"]')).toBe(
        `${verdict} the 5% target`,
      );
    },
  );
});
