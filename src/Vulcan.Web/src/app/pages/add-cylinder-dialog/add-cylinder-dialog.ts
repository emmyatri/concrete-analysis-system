import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, model, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidatorFn,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';

import { ApiProblem, describeApiError, matchFieldErrors } from '../../core/api/api-errors';
import { TestAge } from '../../core/api/models';
import { VulcanApiService } from '../../core/api/vulcan-api.service';
import { FieldIssues } from '../../core/validation/field-issues';
import { numberField } from '../../core/validation/number-field';
import { parseNumber } from '../../core/validation/parse-number';
import { AppField } from '../../shared/app-field/app-field';

export const TEST_AGES: { label: string; value: TestAge }[] = [
  { label: '3 days', value: 'Day3' },
  { label: '7 days', value: 'Day7' },
  { label: '28 days', value: 'Day28' },
];

const LABEL_MAX = 20;

/** Required, up to 20 characters once trimmed (schema.md, Specimen.Label). Unique per batch is checked by the server. */
export const labelField: ValidatorFn = (control) => {
  const value = String(control.value ?? '').trim();
  if (!value) return { required: true };
  if (value.length > LABEL_MAX) return { label: { message: `Max ${LABEL_MAX} characters` } };
  return null;
};

/**
 * Add a test cylinder to a batch (M1-61, mockup "04 · Add cylinder dialog"). A popup on Batch Detail.
 * Save adds the cylinder and closes; Save & add another keeps the sizes and test age so a set of
 * cylinders is quick to enter. Batch Detail reloads its cylinders when the dialog closes.
 */
@Component({
  selector: 'app-add-cylinder-dialog',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    AppField,
  ],
  templateUrl: './add-cylinder-dialog.html',
  styleUrl: './add-cylinder-dialog.scss',
  providers: [FieldIssues],
})
export class AddCylinderDialog {
  private readonly api = inject(VulcanApiService);
  protected readonly issues = inject(FieldIssues);

  readonly batchNumber = input.required<string>();

  /** Open or closed. Batch Detail sets it to true; Save, Cancel, Esc and the X set it back to false. */
  readonly visible = model(false);

  /** Each cylinder that saved, by label. */
  readonly saved = output<string>();

  protected readonly testAges = TEST_AGES;

  protected readonly form = inject(NonNullableFormBuilder).group({
    label: ['', [labelField]],
    lengthIn: ['', [numberField(2)]],
    widthIn: ['', [numberField(2)]],
    testAgeDays: new FormControl<TestAge | null>(null, [testAgeRequired]),
  });

  private readonly formChange = toSignal(this.form.events, { initialValue: null });

  protected readonly saving = signal(false);
  protected readonly saveErrors = signal<string[]>([]);
  /** Labels saved with "Save & add another" on this visit, shown so the operator can keep count. */
  protected readonly added = signal<string[]>([]);

  protected readonly canSave = computed(() => {
    this.formChange();
    return this.form.valid;
  });

  protected readonly saveMessage = computed(() => {
    const summary = this.issues.summary();
    if (this.issues.errors() > 0 || this.canSave()) return summary;
    return summary ? `Fill in every field to save · ${summary}` : 'Fill in every field to save';
  });

  protected save(then: 'close' | 'another'): void {
    if (!this.canSave() || this.saving()) return;
    this.saving.set(true);
    this.saveErrors.set([]);

    const v = this.form.getRawValue();
    const request = {
      label: v.label.trim(),
      lengthIn: parseNumber(v.lengthIn) as number,
      widthIn: parseNumber(v.widthIn) as number,
      testAgeDays: v.testAgeDays as TestAge,
    };
    this.api.createSpecimen(this.batchNumber(), request).subscribe({
      next: ({ label }) => {
        this.saving.set(false);
        this.saved.emit(label);
        if (then === 'close') {
          this.visible.set(false);
          return;
        }
        // Ready for the next one: new label, same sizes and test age.
        this.added.update((labels) => [...labels, label]);
        this.form.controls.label.reset('');
        document.getElementById('cylinder-label')?.focus();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.showProblem(error);
      },
    });
  }

  private showProblem(error: unknown): void {
    if (error instanceof HttpErrorResponse && error.status === 404) {
      this.saveErrors.set([
        `There's no batch ${this.batchNumber()}. It may have been removed; reload the page.`,
      ]);
      return;
    }
    const problem: ApiProblem = describeApiError(error);
    if (problem.kind !== 'validation') {
      this.saveErrors.set([problem.message]);
      return;
    }
    const controls = new Map<string, AbstractControl>([
      ['Specimen.Label', this.form.controls.label],
      ['Specimen.LengthIn', this.form.controls.lengthIn],
      ['Specimen.WidthIn', this.form.controls.widthIn],
      ['Specimen.TestAgeDays', this.form.controls.testAgeDays],
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

  /** Starts fresh each time it opens. */
  protected reset(): void {
    this.form.reset({ label: '', lengthIn: '', widthIn: '', testAgeDays: null });
    this.saveErrors.set([]);
    this.added.set([]);
  }
}

/** The test age dropdown starts empty and must be picked. */
function testAgeRequired(control: AbstractControl) {
  return control.value ? null : { required: true };
}
