import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { providePrimeNG } from 'primeng/config';

import {
  clearSavedMockBatches,
  mockApiInterceptor,
} from '../../core/api/mock/mock-api.interceptor';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { USE_MOCKS } from '../../core/api/use-mocks';
import { SyntheticDataSetting } from '../../core/session/synthetic-data';
import { VulcanPreset } from '../../theme/vulcan-preset';
import { PlaceholderPage } from '../placeholder-page';
import { BatchSearchPage } from './batch-search';

describe('BatchSearchPage', () => {
  let root: HTMLElement;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    clearSavedMockBatches();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: 'batches', component: BatchSearchPage },
            {
              path: 'batches/:batchNumber',
              component: PlaceholderPage,
              data: { heading: 'Batch detail' },
            },
          ],
          withComponentInputBinding(),
        ),
        provideHttpClient(withInterceptors([mockApiInterceptor])),
        providePrimeNG({ theme: { preset: VulcanPreset } }),
        { provide: USE_MOCKS, useValue: true },
      ],
    });
    TestBed.inject(ReferenceDataService).load();
    harness = await RouterTestingHarness.create('/batches');
    root = harness.routeNativeElement as HTMLElement;
  });

  const settle = async () => {
    await harness.fixture.whenStable();
    harness.detectChanges();
  };
  const search = async (value: string) => {
    const input = root.querySelector<HTMLInputElement>('[data-testid="search-input"]')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    root.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await settle();
  };
  const message = () => root.querySelector('[data-testid="search-message"]')?.textContent?.trim();
  const batchNumbers = () =>
    [...root.querySelectorAll('[data-testid="results"] tbody .batch-number')].map((el) =>
      el.textContent?.trim(),
    );

  it('asks for a batch number before the first search', () => {
    expect(message()).toBe('Type a batch number to search');
  });

  it('finds batches by any part of the number, ignoring case', async () => {
    await search('syn-00');

    expect(batchNumbers()).toEqual([
      'VM-SYN-009',
      'VM-SYN-008',
      'VM-SYN-007',
      'VM-SYN-006',
      'VM-SYN-005',
      'VM-SYN-004',
      'VM-SYN-003',
      'VM-SYN-002',
      'VM-SYN-001',
    ]);
  });

  it('shows the batch details in each row, with out-of-range w/c and a missed target in amber', async () => {
    await search('VM-SYN-006');
    const cells = [...root.querySelectorAll('[data-testid="results"] tbody td')].map((td) =>
      td.textContent?.replace(/\s+/g, ' ').trim(),
    );

    expect(root.querySelector('[data-testid="results"] .badge-synthetic')?.textContent).toBe(
      'SYNTHETIC',
    );
    expect(cells.slice(1)).toEqual([
      '06/21/26',
      'Type III',
      '6,200',
      '0.603 ⚠',
      '4,461 ⚠',
      '9 / 9',
      '2 warnings',
    ]);
  });

  it('says when nothing matches', async () => {
    await search('xyz');

    expect(message()).toBe("No batches match 'xyz'");
  });

  it('hides synthetic batches when the switch is off', async () => {
    await search('vm-');
    TestBed.inject(SyntheticDataSetting).show.set(false);
    await settle();

    expect(batchNumbers()).toEqual(['VM-2026-0142', 'VM-2026-0141', 'VM-2026-0139']);
  });

  it('says synthetic data is hidden when that is why nothing matches', async () => {
    TestBed.inject(SyntheticDataSetting).show.set(false);
    await search('008');

    expect(message()).toBe(
      "No batches match '008'. Synthetic data is hidden; turn it on in the top bar to include test batches.",
    );
  });

  it('opens Batch Detail when a row is clicked', async () => {
    await search('0141');
    root.querySelector<HTMLElement>('[data-testid="results"] tbody tr')!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/batches/VM-2026-0141');
  });

  it('goes back to the hint after Clear', async () => {
    await search('xyz');
    [...root.querySelectorAll<HTMLButtonElement>('button')]
      .find((b) => b.textContent?.trim() === 'Clear')!
      .click();
    await settle();

    expect(message()).toBe('Type a batch number to search');
    expect(root.querySelector<HTMLInputElement>('[data-testid="search-input"]')!.value).toBe('');
  });
});
