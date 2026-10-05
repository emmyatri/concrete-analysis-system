import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { AppField, AppFieldState } from '../../shared/app-field/app-field';
import { FieldIssues } from '../../core/validation/field-issues';
import type { ValidationResult } from '../../core/validation/validation-result';
import { ValidationService } from '../../core/validation/validation.service';
import { parseNumber } from '../../core/validation/parse-number';

import { ReferenceDataService } from '../../core/api/reference-data.service';
import { BATCH_NUMBER_HINT, CEMENT_TYPES, SCM_TYPES, createBatchForm } from './batch-form';
import { calculateComposition } from './composition-calc';

/**
 * "New batch" screen (M1-43, mockup "03 · Batch Entry"): batch details, mix recipe and cement oxides,
 * with the composition totals calculated live.
 */
@Component({
  selector: 'app-batch-entry',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    DatePickerModule,
    InputTextModule,
    SelectModule,
    AppField,
  ],
  templateUrl: './batch-entry.html',
  styleUrl: './batch-entry.scss',
  // Every AppField on the page reports to this, for the summary next to Save.
  providers: [FieldIssues],
})
export class BatchEntry {
  private readonly router = inject(Router);
  private readonly referenceData = inject(ReferenceDataService);
  private readonly validation = inject(ValidationService);
  protected readonly issues = inject(FieldIssues);

  protected readonly cementTypes = CEMENT_TYPES;
  protected readonly scmTypes = SCM_TYPES;
  protected readonly batchNumberHint = BATCH_NUMBER_HINT;
  protected readonly now = new Date();

  protected readonly form = createBatchForm(inject(NonNullableFormBuilder));

  /** Re-runs the calculated values below on any form change. */
  private readonly formChange = toSignal(this.form.events, { initialValue: null });

  /** Save stays disabled until every field is filled in and nothing is red. Warnings still save. */
  protected readonly canSave = computed(() => {
    this.formChange();
    return this.form.valid && this.totalCheck().severity !== 'Reject';
  });

  /**
   * The line next to Save: what blocks it. When nothing is red yet but fields are still empty,
   * say so instead of leaving Save disabled with no reason.
   */
  protected readonly saveMessage = computed(() => {
    const summary = this.issues.summary();
    if (this.issues.errors() > 0 || this.canSave()) return summary;
    return summary ? `Fill in every field to save · ${summary}` : 'Fill in every field to save';
  });

  // ---------- Calculated · not stored ----------

  protected readonly totals = computed(() => {
    this.formChange();
    const v = this.form.controls.composition.getRawValue();
    return calculateComposition(
      {
        cementPct: parseNumber(v.cementPct),
        waterPct: parseNumber(v.waterPct),
        coarseAggregatePct: parseNumber(v.coarseAggregatePct),
        fineAggregatePct: parseNumber(v.fineAggregatePct),
        scmPct: parseNumber(v.scmPct),
        scmType: v.scmType,
      },
      this.referenceData.specificGravities(),
    );
  });

  /** Total over 100% is an error, like a red field: it blocks Save. */
  protected readonly totalCheck = computed(() =>
    this.check('Composition.TotalPct', this.totals().totalPct),
  );

  /** Water / cementitious outside the normal range is a warning; it still saves. */
  protected readonly ratioCheck = computed(() => {
    const ratio = this.totals().waterCementRatio;
    return ratio === null ? NO_ISSUE : this.check('Composition.WaterCementRatio', ratio);
  });

  private check(limitKey: string, value: number): ValidationResult {
    return this.validation.evaluate(limitKey, value, this.referenceData.limits());
  }

  protected readonly mix = computed(() => {
    this.formChange();
    const v = this.form.controls.composition.getRawValue();
    const part = (label: string, cls: string, text: string) => ({
      label,
      cls,
      pct: Math.max(0, parseNumber(text) ?? 0),
    });
    return [
      part('Cement', 'mix-cement', v.cementPct),
      part('Water', 'mix-water', v.waterPct),
      part('Coarse', 'mix-coarse', v.coarseAggregatePct),
      part('Fine', 'mix-fine', v.fineAggregatePct),
      part('SCM', 'mix-scm', v.scmPct),
    ];
  });

  constructor() {
    // The calculated tiles count toward "1 error blocks saving · 2 warnings" like any field.
    this.issues.register(computed(() => toState(this.totalCheck())));
    this.issues.register(computed(() => toState(this.ratioCheck())));

    // The SCM type rule depends on SCM %, so re-check it when SCM % changes, and show the result
    // on SCM type right away (it may never have been clicked).
    const { scmPct, scmType } = this.form.controls.composition.controls;
    scmPct.valueChanges.subscribe(() => {
      scmType.markAsTouched();
      scmType.updateValueAndValidity();
    });
  }

  protected cancel(): void {
    this.router.navigate(['/']);
  }
}

const NO_ISSUE: ValidationResult = { severity: null, message: null };

function toState(result: ValidationResult): AppFieldState {
  if (result.severity === 'Reject') return 'error';
  if (result.severity === 'Warn') return 'warn';
  return 'normal';
}
