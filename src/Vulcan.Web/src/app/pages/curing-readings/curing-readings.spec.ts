import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { providePrimeNG } from 'primeng/config';
import { firstValueFrom } from 'rxjs';

import {
  clearSavedMockBatches,
  mockApiInterceptor,
} from '../../core/api/mock/mock-api.interceptor';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { USE_MOCKS } from '../../core/api/use-mocks';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { CuringReadingsPage, toLocalInput } from './curing-readings';

const GOOD = ['78.4', '68.9', '3.0', '96.1', '14.88', '418.5'];

describe('CuringReadingsPage', () => {
  let root: HTMLElement;
  let harness: RouterTestingHarness;

  /** useMocks true: answered by the fake data. False: answered by hand with HttpTestingController. */
  const open = async (url = '/curing?batch=VM-SYN-006&cylinder=D3-1', useMocks = true) => {
    clearSavedMockBatches();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [{ path: 'curing', component: CuringReadingsPage }],
          withComponentInputBinding(),
        ),
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        provideHttpClientTesting(),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
        { provide: USE_MOCKS, useValue: useMocks },
      ],
    });
    if (useMocks) TestBed.inject(ReferenceDataService).load();
    harness = await RouterTestingHarness.create(url);
    root = harness.routeNativeElement as HTMLElement;
    await settle();
  };

  const settle = async () => {
    await harness.fixture.whenStable();
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };
  const text = (selector: string) =>
    root.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const existingRows = () => [...root.querySelectorAll('[data-testid="existing-row"]')];
  const newRows = () => [...root.querySelectorAll<HTMLElement>('[data-testid="new-row"]')];
  const inputs = (row: HTMLElement) => [...row.querySelectorAll<HTMLInputElement>('input')];
  const status = (row: HTMLElement) =>
    row.querySelector('[data-testid="row-status"]')?.textContent?.trim() ?? '';
  const messages = () =>
    [...root.querySelectorAll('[data-testid="messages"] li')].map((li) =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );

  /** Types into a new row: time taken (blank keeps the default) then the six measurements. */
  const fill = async (row: HTMLElement, values: string[], takenAt = '') => {
    const [time, ...cells] = inputs(row);
    if (takenAt) {
      time.value = takenAt;
      time.dispatchEvent(new Event('input'));
    }
    values.forEach((value, i) => {
      cells[i].value = value;
      cells[i].dispatchEvent(new Event('input'));
    });
    await settle();
  };
  const pressEnter = async (row: HTMLElement) => {
    inputs(row)[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    await settle();
  };
  const readings = async () =>
    (await firstValueFrom(TestBed.inject(VulcanApiService).getBatch('VM-SYN-006'))).specimens.find(
      (s) => s.label === 'D3-1',
    )!.curingReadings;

  afterEach(() => harness?.fixture.destroy());

  it('shows the saved readings oldest first, with an empty row at the bottom', async () => {
    await open();

    expect(existingRows().length).toBe(7);
    expect(existingRows()[0].textContent).toContain('69.5 °F');
    expect(existingRows()[6].textContent).toContain('71.6 °F');
    // Each saved value shows its unit, formatted like Batch Detail.
    expect(
      [...existingRows()[0].querySelectorAll('td')].slice(2, 8).map((td) => td.textContent?.trim()),
    ).toEqual(['78.4 °F', '69.5 °F', '91.6%', '96.1%', '15.98 Ω/ft', '416.8 ppm']);
    expect(newRows().length).toBe(1);
    expect(newRows()[0].querySelector('td')?.textContent?.trim()).toBe('8');
    // Time taken starts at now.
    expect(inputs(newRows()[0])[0].value).toBe(toLocalInput(new Date()));
  });

  it('shows the ranges above the grid', async () => {
    await open();

    expect(text('[data-testid="ranges"]')).toBe(
      'Internal 0–200 °F · Ambient 0–150 °F (normal 60–80) · Moisture (% of sample weight) 0–15% (normal 2–4) · RH 0–100% · ' +
        'Resistivity ≥ 0.01 ohms/ft · CO₂ ≥ 0 ppm',
    );
  });

  it('Enter saves the row and starts a new one', async () => {
    await open();
    await fill(newRows()[0], GOOD);
    await pressEnter(newRows()[0]);

    expect(status(newRows()[0])).toBe('Saved');
    // Once saved, the row reads like the ones above it: no boxes, units on every value.
    expect(inputs(newRows()[0])).toHaveLength(0);
    expect(
      [...newRows()[0].querySelectorAll('td')].slice(2, 8).map((td) => td.textContent?.trim()),
    ).toEqual(['78.4 °F', '68.9 °F', '3.0%', '96.1%', '14.88 Ω/ft', '418.5 ppm']);
    expect(newRows().length).toBe(2);
    expect(newRows()[1].querySelector('td')?.textContent?.trim()).toBe('9');
    expect((await readings()).length).toBe(8);
  });

  it('accepts internal temperature with one decimal place', async () => {
    await open();
    await fill(newRows()[0], ['72.1', '68.9', '3.0', '96.1', '14.88', '418.5']);

    expect(inputs(newRows()[0])[1].classList).not.toContain('cell--error');

    await pressEnter(newRows()[0]);

    expect(status(newRows()[0])).toBe('Saved');
    expect((await readings()).length).toBe(8);
    expect((await readings()).at(-1)?.internalTempF).toBe(72.1);
  });

  it('rejects internal temperature with more than one decimal place', async () => {
    await open();
    await fill(newRows()[0], ['72.12', '68.9', '3.0', '96.1', '14.88', '418.5']);

    expect(inputs(newRows()[0])[1].classList).toContain('cell--error');

    await pressEnter(newRows()[0]);

    expect(status(newRows()[0])).toBe('Not saved');
    expect(messages()).toEqual(['Row 8 · Internal — max 1 decimal place.']);
    expect((await readings()).length).toBe(7);
  });

  it('blocks ambient 720 and says why', async () => {
    await open();
    await fill(newRows()[0], ['78.4', '720', '3.0', '96.1', '14.88', '418.5']);

    expect(inputs(newRows()[0])[2].classList).toContain('cell--error');

    await pressEnter(newRows()[0]);

    expect(status(newRows()[0])).toBe('Not saved');
    expect(messages()).toEqual(['Row 8 · Ambient 720 °F — must be 0–150 °F.']);
    expect((await readings()).length).toBe(7);
  });

  it('saves ambient 84.2 in amber with a warning', async () => {
    await open();
    await fill(newRows()[0], ['78.4', '84.2', '3.0', '96.1', '14.88', '418.5']);

    expect(inputs(newRows()[0])[2].classList).toContain('cell--warn');

    await pressEnter(newRows()[0]);

    expect(status(newRows()[0])).toBe('Saved · flagged');
    expect(newRows()[0].querySelector('.flagged')?.textContent?.trim()).toBe('84.2 °F ⚠');
    const batch = await firstValueFrom(TestBed.inject(VulcanApiService).getBatch('VM-SYN-006'));
    expect(batch.warnings.some((w) => w.entityType === 'CuringReading' && w.value === 84.2)).toBe(
      true,
    );
  });

  it('accepts internal temperature entered without a leading zero', async () => {
    await open();
    await fill(newRows()[0], ['.5', '68.9', '3.0', '96.1', '14.88', '418.5']);

    expect(inputs(newRows()[0])[1].classList).not.toContain('cell--error');

    await pressEnter(newRows()[0]);

    expect(status(newRows()[0])).toBe('Saved');
    expect((await readings()).length).toBe(8);
    expect((await readings()).at(-1)?.internalTempF).toBe(0.5);
  });

  it('blocks a time before the batch was cast', async () => {
    await open();
    await fill(newRows()[0], GOOD, '2026-06-20T08:00');
    await pressEnter(newRows()[0]);

    expect(status(newRows()[0])).toBe('Not saved');
    expect(messages()).toEqual(["Row 8 · Time taken — can't be before the batch was cast."]);
    expect(inputs(newRows()[0])[0].classList).toContain('cell--error');
  });

  it('blocks a time in the future', async () => {
    await open();
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    await fill(newRows()[0], GOOD, toLocalInput(tomorrow));
    await pressEnter(newRows()[0]);

    expect(messages()).toEqual(["Row 8 · Time taken — can't be in the future."]);
  });

  it('marks empty cells only after trying to save', async () => {
    await open();
    await fill(newRows()[0], ['78.4']);

    expect(inputs(newRows()[0])[2].classList).not.toContain('cell--error');

    await pressEnter(newRows()[0]);

    expect(inputs(newRows()[0])[2].classList).toContain('cell--error');
    expect(messages()).toContain('Row 8 · Ambient — required.');
  });

  it('Save all saves the good rows and keeps the bad ones, so one bad row blocks nothing else', async () => {
    await open();
    await fill(newRows()[0], ['78.4', '720', '3.0', '96.1', '14.88', '418.5']);
    root.querySelector<HTMLButtonElement>('[data-testid="add-row"] button')!.click();
    await settle();
    await fill(newRows()[1], GOOD);
    root.querySelector<HTMLButtonElement>('[data-testid="save-all"] button')!.click();
    await settle();

    // The good row saved, so a fresh row was added for the next reading.
    expect(newRows().map(status)).toEqual(['Not saved', 'Saved', '']);
    expect((await readings()).length).toBe(8);
  });

  it("shows the server's message on the cell when it refuses a value", async () => {
    await open('/curing?batch=VM-SYN-006&cylinder=D3-1', false);
    const http = TestBed.inject(HttpTestingController);
    http
      .match(() => true)
      .forEach((request) => {
        if (request.request.url === '/api/batches/VM-SYN-006') {
          request.flush({
            id: 6,
            batchNumber: 'VM-SYN-006',
            castAt: '2026-06-21T12:00:00Z',
            specimens: [{ id: 601, label: 'D3-1', curingReadings: [] }],
            warnings: [],
          });
        } else if (request.request.url === '/api/batches') {
          request.flush([]);
        }
      });
    await settle();
    await fill(newRows()[0], GOOD);
    await pressEnter(newRows()[0]);
    http
      .expectOne('/api/specimens/601/readings')
      .flush(
        { title: 'Bad', status: 400, errors: { 'Curing.MoisturePct': ['Moisture is too wet'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle();

    expect(status(newRows()[0])).toBe('Not saved');
    expect(messages()).toEqual([
      'Row 1 · Moisture (% of sample weight) — Moisture is too wet',
    ]);
    expect(inputs(newRows()[0])[3].classList).toContain('cell--error');
  });

  it('opens on the first cylinder when none is given', async () => {
    await open('/curing?batch=VM-SYN-006');

    expect(text('#grid-title')).toBe('D3-1 · readings');
  });

  it('asks for a batch first', async () => {
    await open('/curing');

    expect(text('[data-testid="curing-message"]')).toBe('Pick a batch to enter readings.');
  });
});
