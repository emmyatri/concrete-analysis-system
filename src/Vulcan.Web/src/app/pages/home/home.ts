import { DatePipe, formatNumber } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { catchError, map, of, startWith, switchMap } from 'rxjs';

import { describeApiError } from '../../core/api/api-errors';
import { earlyWarningsShortfallFirst } from '../../core/api/early-warnings';
import { CEMENT_LABELS } from '../../core/api/cement-labels';
import { BatchSummary, EarlyWarningBatch } from '../../core/api/models';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { SyntheticDataSetting } from '../../core/session/synthetic-data';
import { DATE_GROUPS, DateGroup, dateGroup } from '../../shared/date-group/date-group';
import { TechnicianSession } from '../../core/session/technician-session';

/** How many batches Home lists. Search finds the rest. */
const RECENT_COUNT = 8;

/** A recent batch with the date heading it sits under. */
type RecentRow = BatchSummary & { group: DateGroup; groupOrder: number };

type Recent =
  | { status: 'loading' }
  | { status: 'loaded'; batches: BatchSummary[] }
  | { status: 'failed'; message: string };

type AtRisk =
  | { status: 'loading' }
  | { status: 'loaded'; batches: EarlyWarningBatch[] }
  | { status: 'failed'; message: string };

type AtRiskRow = EarlyWarningBatch & { shortfall: string; topReason: string };

/**
 * Where the operator lands after signing in (M1-64, mockup "02 · Home"): a greeting, the three
 * everyday actions, and the most recent batches.
 */
@Component({
  selector: 'app-home',
  imports: [DatePipe, RouterLink, ButtonModule, TableModule],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class HomePage {
  private readonly api = inject(VulcanApiService);
  private readonly router = inject(Router);
  private readonly session = inject(TechnicianSession);
  private readonly synthetic = inject(SyntheticDataSetting);

  protected cementLabel(type: BatchSummary['cementType']): string {
    return CEMENT_LABELS[type] ?? type;
  }

  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const partOfDay = hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening';
    const name = this.session.name();
    return name ? `${partOfDay}, ${name}` : partOfDay;
  });

  /** Newest first. Follows the Synthetic data switch. */
  protected readonly recent = toSignal(
    toObservable(this.synthetic.show).pipe(
      switchMap((includeSynthetic) =>
        this.api.getBatches({ includeSynthetic }).pipe(
          map((batches): Recent => ({ status: 'loaded', batches: batches.slice(0, RECENT_COUNT) })),
          catchError((error: unknown) =>
            of<Recent>({ status: 'failed', message: describeApiError(error).message }),
          ),
          startWith<Recent>({ status: 'loading' }),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as Recent },
  );

  /**
   * Grouped by cast date under Today, Yesterday, Last week… like File Explorer. The table groups by
   * groupOrder (not the heading text) so the headings stay newest first instead of alphabetical.
   */
  protected readonly rows = computed((): RecentRow[] => {
    const recent = this.recent();
    if (recent.status !== 'loaded') return [];
    const now = new Date();
    return recent.batches.map((batch) => {
      const group = dateGroup(new Date(batch.castAt), now);
      return { ...batch, group, groupOrder: DATE_GROUPS.indexOf(group) };
    });
  });

  /** At-risk batches only (FR-16). Follows the Synthetic data switch. */
  protected readonly atRisk = toSignal(
    toObservable(this.synthetic.show).pipe(
      switchMap((includeSynthetic) =>
        this.api.getEarlyWarnings(includeSynthetic).pipe(
          map((batches): AtRisk => ({
            status: 'loaded',
            batches: batches.filter((batch) => batch.atRisk),
          })),
          catchError((error: unknown) =>
            of<AtRisk>({ status: 'failed', message: describeApiError(error).message }),
          ),
          startWith<AtRisk>({ status: 'loading' }),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as AtRisk },
  );

  protected readonly atRiskRows = computed((): AtRiskRow[] => {
    const atRisk = this.atRisk();
    if (atRisk.status !== 'loaded') return [];
    return atRisk.batches.map((batch) => ({
      ...batch,
      shortfall: shortfallLabel(batch),
      topReason: earlyWarningsShortfallFirst(batch.earlyWarnings)[0]?.warning ?? '',
    }));
  });

  protected readonly atRiskMessage = computed(() => {
    const atRisk = this.atRisk();
    if (atRisk.status === 'loading') return 'Loading batches at risk…';
    if (atRisk.status === 'failed') return atRisk.message;
    return atRisk.batches.length === 0 ? 'No batches at risk.' : '';
  });

  /** "Loading…" or the error, while there's no list to show. */
  protected readonly message = computed(() => {
    const recent = this.recent();
    if (recent.status === 'loading') return 'Loading recent batches…';
    if (recent.status === 'failed') return recent.message;
    return recent.batches.length === 0
      ? 'No batches yet. Use New batch to enter the first one.'
      : '';
  });

  protected open(batch: BatchSummary | EarlyWarningBatch): void {
    this.router.navigate(['/batches', batch.batchNumber]);
  }
}

function shortfallLabel(batch: EarlyWarningBatch): string {
  if (batch.shortfallPsi == null) return '';
  return `${formatNumber(batch.shortfallPsi, 'en-US', '1.0-0')} PSI short of target`;
}
