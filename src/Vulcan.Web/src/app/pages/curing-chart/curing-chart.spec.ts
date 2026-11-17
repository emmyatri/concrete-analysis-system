import { Component, input } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';

import { MOCK_BATCH_DETAILS } from '../../core/api/mock/mock-batch-details';
import { mockStrength } from '../../core/api/mock/mock-strength';
import { CuringPoint, SavedWarning } from '../../core/api/models';
import { CuringChart } from './curing-chart';

/** Stands in for p-chart, since jsdom can't draw on a canvas. */
// eslint-disable-next-line @angular-eslint/component-selector -- must match PrimeNG's selector
@Component({ selector: 'p-chart', template: '<canvas></canvas>' })
class FakeChart {
  readonly type = input<string>();
  readonly data = input<unknown>();
  readonly options = input<unknown>();
  readonly plugins = input<unknown>();
  readonly height = input<string>();
}

describe('CuringChart', () => {
  let fixture: ComponentFixture<CuringChart>;

  const batch = MOCK_BATCH_DETAILS.find((b) => b.batchNumber === 'VM-SYN-006')!;
  const allPoints = mockStrength(batch).curingPoints;
  const readings = batch.specimens.flatMap((s) =>
    s.curingReadings.map((reading) => ({ label: s.label, reading })),
  );

  const caption = (points: CuringPoint[], rows = readings) => {
    TestBed.overrideComponent(CuringChart, {
      set: { imports: [FakeChart, SelectModule, FormsModule] },
    });
    fixture = TestBed.createComponent(CuringChart);
    fixture.componentRef.setInput('curingPoints', points);
    fixture.componentRef.setInput('readings', rows);
    fixture.componentRef.setInput('castAt', batch.castAt);
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement)
      .querySelector('.curing-caption')
      ?.textContent?.trim();
  };

  it('on the report, is titled by its measurement with no selector or Expand', () => {
    TestBed.overrideComponent(CuringChart, {
      set: { imports: [FakeChart, SelectModule, FormsModule] },
    });
    fixture = TestBed.createComponent(CuringChart);
    fixture.componentRef.setInput('curingPoints', allPoints);
    fixture.componentRef.setInput('readings', readings);
    fixture.componentRef.setInput('castAt', batch.castAt);
    fixture.componentRef.setInput('initialMeasurement', 'moisturePct');
    fixture.componentRef.setInput('printMode', true);
    fixture.componentRef.setInput('expandable', false);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelector('.chart-title')?.textContent?.trim()).toBe(
      'Moisture (% of sample weight)',
    );
    expect(root.querySelector('p-select')).toBeNull();
    expect(root.querySelector('[data-testid="expand-curing"]')).toBeNull();
  });

  it('says when there are no readings yet', () => {
    expect(caption([], [])).toBe('No curing readings yet');
  });

  it('lists the hours when there are only two reading times', () => {
    const firstTwoTimes = [...new Set(allPoints.map((p) => p.time))].slice(0, 2);

    expect(caption(allPoints.filter((p) => firstTwoTimes.includes(p.time)))).toBe(
      '2 readings so far (0 h and 12 h)',
    );
  });

  it('describes the line and band once there are three or more reading times', () => {
    expect(caption(allPoints)).toBe(
      'Line = average of 9 cylinders · band = lowest to highest · readings so far: 0–672h',
    );
  });

  it('says "cylinder" for a single cylinder', () => {
    const one = allPoints.filter((p) => p.specimenLabel === 'D3-1');
    const oneRows = readings.filter((r) => r.label === 'D3-1');

    expect(caption(one, oneRows)).toContain('average of 1 cylinder ·');
  });

  it('marks a saved warning only on its own measurement', () => {
    const flagged = readings[0];
    const warning: SavedWarning = {
      id: 1,
      entityType: 'CuringReading',
      entityId: flagged.reading.id,
      fieldName: 'AmbientTempF',
      limitKey: 'Curing.AmbientTempF',
      value: 85,
      message: 'Ambient temperature is outside the normal range [60-80]',
      createdAtUtc: batch.createdAtUtc,
    };
    caption(allPoints);
    fixture.componentRef.setInput('warnings', [warning]);
    const key = `${flagged.label} | ${Date.parse(flagged.reading.takenAt)}`;

    fixture.componentInstance.selectedMeasurement.set('ambientTempF');
    expect(fixture.componentInstance.flaggedReadings().get(key)).toBe(warning.message);

    fixture.componentInstance.selectedMeasurement.set('internalTempF');
    expect(fixture.componentInstance.flaggedReadings().size).toBe(0);
  });
});
