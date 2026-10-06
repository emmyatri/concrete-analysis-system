import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, model, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidatorFn,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';

import { ApiProblem, describeApiError, matchFieldErrors } from '../../core/api/api-errors';
import { PendingBreak, RecordWarning } from '../../core/api/models';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { FieldIssues } from '../../core/validation/field-issues';
import { numberField } from '../../core/validation/number-field';
import { parseNumber } from '../../core/validation/parse-number';
import { AppField } from '../../shared/app-field/app-field';

/** What Break results shows after a save. */
export interface BreakSaved {
  batchNumber: string;
  label: string;
  strengthPsi: number;
  warnings: RecordWarning[];
}

export const TEST_AGE_DAYS = { Day3: 3, Day7: 7, Day28: 28 } as const;

/** Midnight at the start of a date's day, local time. */
export function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

/** Whole days from the pour to now: 0 on the day it was cast. */
export function ageInDays(castAt: string, now = new Date()): number {
  return Math.round(
    (startOfDay(now).getTime() - startOfDay(new Date(castAt)).getTime()) / 86_400_000,
  );
}

/** Required, not before the cast date, not after today. Days are compared, not times. */
export function breakDateField(castAt: () => string | undefined): ValidatorFn {
  return (control) => {
    const value = control.value as Date | null;
    if (!value) return { required: true };
    const cast = castAt();
    if (cast && startOfDay(value) < startOfDay(new Date(cast))) {
      return { breakDate: { message: "Can't be before the cast date" } };
    }
    if (startOfDay(value) > startOfDay(new Date())) {
      return { breakDate: { message: "Can't be in the future" } };
    }
    return null;
  };
}

/**
 * Record the strength of one crushed cylinder (M1-62, mockup "05 · Break dialog"). A popup on Break results.
 * Below 3,000 PSI is amber but still saves: failing breaks have to be kept.
 */
@Component({
  selector: 'app-break-dialog',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    AppField,
  ],
  templateUrl: './break-dialog.html',
  styleUrl: './break-dialog.scss',
  providers: [FieldIssues],
})
export class BreakDialog {
  private readonly api = inject(VulcanApiService);
  protected readonly issues = inject(FieldIssues);

  /** The cylinder being broken. Set it before opening. */
  readonly cylinder = input<PendingBreak | null>(null);

  /** Open or closed. Break results sets it to true; Save, Cancel, Esc and the X set it back to false. */
  readonly visible = model(false);

  readonly saved = output<BreakSaved>();
  /** The server says this cylinder is already broken (409), so Break results should reload its list. */
  readonly stale = output<void>();

  protected readonly form = inject(NonNullableFormBuilder).group({
    strengthPsi: ['', [numberField(0)]],
    brokenAt: new FormControl<Date | null>(new Date(), [
      breakDateField(() => this.cylinder()?.castAt),
    ]),
  });

  private readonly formChange = toSignal(this.form.events, { initialValue: null });

  protected readonly saving = signal(false);
  protected readonly saveErrors = signal<string[]>([]);
  /** Set by a 409: nothing more to save for this cylinder. */
  protected readonly alreadyBroken = signal(false);

  protected readonly castDate = computed(() => {
    const cylinder = this.cylinder();
    return cylinder ? startOfDay(new Date(cylinder.castAt)) : null;
  });
  protected readonly today = startOfDay(new Date());
  protected readonly age = computed(() => {
    const cylinder = this.cylinder();
    return cylinder ? ageInDays(cylinder.castAt) : null;
  });
  protected readonly testAge = computed(() => {
    const cylinder = this.cylinder();
    return cylinder ? TEST_AGE_DAYS[cylinder.testAgeDays] : null;
  });

  protected readonly canSave = computed(() => {
    this.formChange();
    return this.form.valid && !this.alreadyBroken();
  });

  protected readonly saveMessage = computed(() => {
    const summary = this.issues.summary();
    if (this.issues.errors() > 0 || this.canSave() || this.alreadyBroken()) return summary;
    return summary ? `Enter the strength to save · ${summary}` : 'Enter the strength to save';
  });

  constructor() {
    // A new cylinder means a new cast date to check the break date against.
    effect(() => {
      this.cylinder();
      this.form.controls.brokenAt.updateValueAndValidity();
    });
  }

  protected save(): void {
    const cylinder = this.cylinder();
    if (!cylinder || !this.canSave() || this.saving()) return;
    this.saving.set(true);
    this.saveErrors.set([]);

    const v = this.form.getRawValue();
    const strengthPsi = parseNumber(v.strengthPsi) as number;
    const request = { brokenAt: breakTime(v.brokenAt as Date).toISOString(), strengthPsi };
    this.api.createBreakResult(cylinder.specimenId, request).subscribe({
      next: ({ warnings }) => {
        this.saving.set(false);
        this.saved.emit({
          batchNumber: cylinder.batchNumber,
          label: cylinder.label,
          strengthPsi,
          warnings,
        });
        this.visible.set(false);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.showProblem(error);
      },
    });
  }

  private showProblem(error: unknown): void {
    if (error instanceof HttpErrorResponse && error.status === 409) {
      this.alreadyBroken.set(true);
      this.saveErrors.set(['This cylinder already has a break result']);
      this.stale.emit();
      return;
    }
    if (error instanceof HttpErrorResponse && error.status === 404) {
      this.saveErrors.set([
        "This cylinder wasn't found. It may have been removed; close this and try again.",
      ]);
      this.stale.emit();
      return;
    }
    const problem: ApiProblem = describeApiError(error);
    if (problem.kind !== 'validation') {
      this.saveErrors.set([problem.message]);
      return;
    }
    const controls = new Map<string, AbstractControl>([
      ['Break.StrengthPsi', this.form.controls.strengthPsi],
      ['Break.BrokenAt', this.form.controls.brokenAt],
    ]);
    const { byKey, unmatched } = matchFieldErrors(problem.fieldErrors, controls.keys());
    for (const [key, messages] of byKey) {
      const control = controls.get(key)!;
      control.setErrors({ ...control.errors, server: messages[0] });
      control.markAsTouched();
    }
    this.saveErrors.set(unmatched.length > 0 || byKey.size > 0 ? unmatched : [problem.message]);
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  /** Starts fresh each time it opens: no strength, broken today. */
  protected reset(): void {
    this.form.reset({ strengthPsi: '', brokenAt: new Date() });
    this.saveErrors.set([]);
    this.alreadyBroken.set(false);
  }
}

/** Broken today: now. Broken on an earlier day: noon that day, since only the date was picked. */
function breakTime(date: Date): Date {
  const now = new Date();
  if (startOfDay(date).getTime() === startOfDay(now).getTime()) return now;
  return new Date(date.getFullYear(), date.getMonth(), date.getDate(), 12, 0);
}
