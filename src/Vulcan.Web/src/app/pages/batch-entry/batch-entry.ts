import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';

import { BATCH_NUMBER_HINT, CEMENT_TYPES, SCM_TYPES, createBatchForm, toNumber } from './batch-form';
import { calculateComposition } from './composition-calc';

/**
 * TEMPORARY: same values as the SCRUM-96 seed (specific-gravity.json).
 * TODO(M1-45): read these from the reference-data service, then delete this. Must not merge as-is.
 */
const TEMP_SPECIFIC_GRAVITIES = new Map<string, number>([
  ['Cement', 3.15],
  ['Water', 1.0],
  ['FlyAsh', 2.35],
  ['Slag', 2.9],
  ['SilicaFume', 2.35],
]);

/**
 * "New batch" screen (M1-43, mockup "03 · Batch Entry"): batch details and mix recipe,
 * with the composition totals calculated live.
 */
@Component({
  selector: 'app-batch-entry',
  imports: [ReactiveFormsModule, RouterLink, ButtonModule, DatePickerModule, InputTextModule, SelectModule],
  templateUrl: './batch-entry.html',
  styleUrl: './batch-entry.scss',
})
export class BatchEntry {
  private readonly router = inject(Router);

  protected readonly cementTypes = CEMENT_TYPES;
  protected readonly scmTypes = SCM_TYPES;
  protected readonly batchNumberHint = BATCH_NUMBER_HINT;
  protected readonly now = new Date();

  protected readonly form = createBatchForm(inject(NonNullableFormBuilder));

  /** Re-runs the calculated values below on any form change. */
  private readonly formChange = toSignal(this.form.events, { initialValue: null });

  // ---------- Calculated · not stored ----------

  protected readonly totals = computed(() => {
    this.formChange();
    const v = this.form.controls.composition.getRawValue();
    return calculateComposition(
      {
        cementPct: toNumber(v.cementPct),
        waterPct: toNumber(v.waterPct),
        coarseAggregatePct: toNumber(v.coarseAggregatePct),
        fineAggregatePct: toNumber(v.fineAggregatePct),
        scmPct: toNumber(v.scmPct),
        scmType: v.scmType,
      },
      TEMP_SPECIFIC_GRAVITIES,
    );
  });

  protected readonly mix = computed(() => {
    this.formChange();
    const v = this.form.controls.composition.getRawValue();
    const part = (label: string, cls: string, text: string) => ({ label, cls, pct: Math.max(0, toNumber(text) ?? 0) });
    return [
      part('Cement', 'mix-cement', v.cementPct),
      part('Water', 'mix-water', v.waterPct),
      part('Coarse', 'mix-coarse', v.coarseAggregatePct),
      part('Fine', 'mix-fine', v.fineAggregatePct),
      part('SCM', 'mix-scm', v.scmPct),
    ];
  });

  constructor() {
    // The SCM type rule depends on SCM %, so re-check it when SCM % changes.
    const { scmPct, scmType } = this.form.controls.composition.controls;
    scmPct.valueChanges.subscribe(() => scmType.updateValueAndValidity());
  }

  protected cancel(): void {
    this.router.navigate(['/']);
  }
}
