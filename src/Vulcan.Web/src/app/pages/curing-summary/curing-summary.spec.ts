import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MOCK_BATCH_DETAILS } from '../../core/api/mock/mock-batch-details';
import { MOCK_SPECIFIC_GRAVITIES, MOCK_VALIDATION_LIMITS } from '../../core/api/mock/mock-data';
import { Specimen } from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { CuringSummary } from './curing-summary';

describe('CuringSummary', () => {
  const render = (specimens: Specimen[], castAt: string) => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    TestBed.inject(ReferenceDataService).load();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/validation-limits').flush(MOCK_VALIDATION_LIMITS);
    http.expectOne('/api/specific-gravities').flush(MOCK_SPECIFIC_GRAVITIES);

    const fixture = TestBed.createComponent(CuringSummary);
    fixture.componentRef.setInput('specimens', specimens);
    fixture.componentRef.setInput('castAt', castAt);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };
  const rows = (root: HTMLElement) =>
    [...root.querySelectorAll('tbody tr')].map((tr) =>
      [...tr.children].map((cell) => cell.textContent?.replace(/\s+/g, ' ').trim()),
    );
  const vmSyn006 = MOCK_BATCH_DETAILS.find((b) => b.batchNumber === 'VM-SYN-006')!;

  it('summarizes the first and third day of readings across all cylinders', () => {
    const root = render(vmSyn006.specimens, vmSyn006.castAt);

    expect(rows(root)).toEqual([
      ['Internal temp (avg)', '81.0 °F', '77.6 °F'],
      ['Temp rise since cast (peak)', '+6.4 °F', '+1.2 °F'],
      ['Ambient temp (avg)', '68.5 °F', '70.0 °F'],
      ['Moisture change (% of sample weight)', '+0.6 percentage points', '+0.9 percentage points'],
      ['Relative humidity (avg)', '95.2%', '95.0%'],
      ['Resistivity change', '+3.4 Ω/ft', '+8.6 Ω/ft'],
      ['CO₂ (avg)', '423 ppm', '419 ppm'],
    ]);
    expect(root.querySelector('.flagged')).toBeNull();
  });

  it('flags a day whose average is outside the Warn range, like the rest of Batch Detail', () => {
    const specimens = vmSyn006.specimens.map((s) => ({
      ...s,
      curingReadings: s.curingReadings.map((r) => ({ ...r, ambientTempF: 85 })),
    }));

    const root = render(specimens, vmSyn006.castAt);

    const flagged = [...root.querySelectorAll('.flagged')];
    expect(flagged.map((f) => f.textContent?.trim())).toEqual(['85.0 °F ⚠', '85.0 °F ⚠']);
    expect(flagged[0].getAttribute('title')).toBe(
      'Ambient temperature is outside the normal range [60-80]',
    );
  });

  it('shows a dash for a day with no readings yet', () => {
    const specimens = vmSyn006.specimens.map((s) => ({
      ...s,
      curingReadings: s.curingReadings.slice(0, 3),
    }));

    const root = render(specimens, vmSyn006.castAt);

    expect(rows(root)[0]).toEqual(['Internal temp (avg)', '81.0 °F', '—']);
  });

  it('says when there are no readings at all', () => {
    const root = render(
      vmSyn006.specimens.map((s) => ({ ...s, curingReadings: [] })),
      vmSyn006.castAt,
    );

    expect(root.querySelector('[data-testid="curing-summary-empty"]')?.textContent?.trim()).toBe(
      'No curing readings yet.',
    );
  });
});
