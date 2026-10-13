import { formatNumber } from '@angular/common';
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import {
  Component,
  DestroyRef,
  input,
  signal,
  effect,
  computed,
  inject,
  untracked,
} from '@angular/core';
import { ValidationService } from '../../core/validation/validation.service';
import type { ValidationLimit } from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { parseNumber } from '../../core/validation/parse-number';
import { FieldIssues } from '../../core/validation/field-issues';

// Possible states for the app-field component.
export type AppFieldState = 'error' | 'warn' | 'normal';

// Keys for which the maximum value should be displayed in the hint.
const KEYS_WITH_DISPLAYED_MAXIMUM = new Set(['Oxide.MagnesiumOxidePct', 'Oxide.SulfurTrioxidePct']);

// Keys that show typical (Warn) and allowed (Reject) ranges in one hint, e.g. sample weight.
const KEYS_WITH_TYPICAL_AND_ALLOWED = new Set(['Batch.FinalWeightLb']);

// Reusable wrapper for a form control with a label and optional hint.
@Component({
  selector: 'app-field',
  templateUrl: './app-field.html',
  styleUrls: ['./app-field.scss'],
  // Apply CSS classes based on the current state of the app-field.
  host: {
    '[class.app-field--error]': "state() === 'error'",
    '[class.app-field--warn]': "state() === 'warn'",
  },
})

// Class representing the app-field component.
export class AppField {
  // Injected services for validation and reference data.
  private readonly validationService = inject(ValidationService);
  private readonly referenceDataService = inject(ReferenceDataService);

  // Inputs for the app-field component.
  readonly label = input.required<string>();
  readonly inputId = input.required<string>();
  readonly control = input.required<AbstractControl>();
  readonly limitKey = input<string>('');
  readonly hint = input<string>('');

  // Snapshot of the control's current state and field value.
  private readonly controlSnapshot = signal({
    // User has not interacted with the control yet.
    touched: false,
    // Control currently has no rejected validation error.
    invalid: false,
    // Current value of the control.
    value: null as unknown,
    // Current validation errors of the control.
    errors: null as ValidationErrors | null,
  });

  // Computed state of the app-field based on the control snapshot.
  readonly state = computed<AppFieldState>(() => {
    const { touched, invalid, errors } = this.controlSnapshot();
    const serverMessage = errors?.['server'];

    // If there is a server-side validation message, the field is in an error state.
    if (typeof serverMessage === 'string') {
      return 'error';
    }

    // If there is no server-side validation message, proceed with local validation.
    const result = this.validationResult();
    if (!touched) {
      return 'normal';
    }
    if (invalid || result.severity === 'Reject') {
      return 'error';
    }
    if (result.severity === 'Warn') {
      return 'warn';
    }
    return 'normal';
  });

  // Computed validation result based on the current control value and validation limits.
  readonly validationResult = computed(() => {
    const limits = this.referenceDataService.limits();
    const limitKey = this.limitKey();
    const rawValue = this.controlSnapshot().value;
    const value = parseNumber(rawValue);
    if (!limitKey || value === null) {
      return { severity: null, message: null };
    }
    return this.validationService.evaluate(limitKey, value, limits);
  });

  // Computed message based on errors or the current validation result.
  readonly message = computed(() => {
    const { touched, errors } = this.controlSnapshot();
    const serverMessage = errors?.['server'];
    if (typeof serverMessage === 'string') {
      return serverMessage;
    }
    // If the control has not been touched, do not show any message.
    if (!touched) {
      return '';
    }
    const localMessage = this.getLocalErrorMessage(errors);
    if (localMessage) {
      return localMessage;
    }
    // If the user leaves the field, show the validation message if any.
    const result = this.validationResult();

    return result.message ?? '';
  });

  // Computed allowed-range hint from ValidationLimit.
  readonly computedHint = computed(() => {
    const limitKey = this.limitKey();
    const hint = this.hint();
    const limits = this.referenceDataService.limits();

    // Filter the validation limits to only include those that match the given key.
    const matchLimits = limits.filter((limit) => limit.key === limitKey);

    // Find the first validation limit with severity 'Reject'.
    const rejectLimit = matchLimits.find((limit) => limit.severity === 'Reject');

    // Find the first validation limit with severity 'Warn'.
    const warnLimit = matchLimits.find((limit) => limit.severity === 'Warn');

    // Show the Reject range when the value is an error. Otherwise, prefer the normal Warn range.
    const result = this.validationResult();

    if (KEYS_WITH_TYPICAL_AND_ALLOWED.has(limitKey) && warnLimit && rejectLimit) {
      return this.formatTypicalAndAllowedHint(warnLimit, rejectLimit) ?? hint;
    }

    const selectedLimit = result.severity === 'Reject' ? rejectLimit : (warnLimit ?? rejectLimit);

    // Return the range hint from the selected limit.
    if (!selectedLimit) {
      return hint;
    }

    // Determine the maximum limit to display based on the severity and the keys that require a displayed maximum.
    const maximumLimit =
      result.severity === 'Reject'
        ? undefined
        : KEYS_WITH_DISPLAYED_MAXIMUM.has(limitKey)
          ? rejectLimit
          : undefined;

    return this.formatLimitHint(selectedLimit, maximumLimit) ?? hint;
  });

  // "20–30 lb typical (12×6 cylinder) · 10–40 allowed" from Warn + Reject rows.
  private formatTypicalAndAllowedHint(
    typicalLimit: ValidationLimit,
    allowedLimit: ValidationLimit,
  ): string | null {
    const { min: typicalMin, max: typicalMax } = typicalLimit;
    const { min: allowedMin, max: allowedMax } = allowedLimit;
    if (typicalMin === null || typicalMax === null || allowedMin === null || allowedMax === null) {
      return null;
    }

    const typicalRange = withLimitUnit(
      `${formatLimitNumber(typicalMin)}–${formatLimitNumber(typicalMax)}`,
      typicalLimit.unit,
    );
    const allowedRange = `${formatLimitNumber(allowedMin)}–${formatLimitNumber(allowedMax)}`;
    return `${typicalRange} typical (12×6 cylinder) · ${allowedRange} allowed`;
  }

  // Formats the range hint like the mockup: "10–15%", "3,000–10,000 PSI", "0.1–4% · max 6%".
  private formatLimitHint(
    normalLimit: ValidationLimit,
    maximumLimit?: ValidationLimit,
  ): string | null {
    const withUnit = (text: string) => withLimitUnit(text, normalLimit.unit);
    const maximumText =
      maximumLimit && maximumLimit.max !== null
        ? ` · max ${withLimitUnit(formatLimitNumber(maximumLimit.max), maximumLimit.unit)}`
        : '';
    const { min, max } = normalLimit;
    if (min !== null && max !== null) {
      return withUnit(`${formatLimitNumber(min)}–${formatLimitNumber(max)}`) + maximumText;
    } else if (min !== null) {
      return withUnit(`≥ ${formatLimitNumber(min)}`);
    } else if (max !== null) {
      return withUnit(`≤ ${formatLimitNumber(max)}`);
    }

    return null;
  }

  // Retrieves a local error message for the given validation errors, if available.
  private getLocalErrorMessage(errors: ValidationErrors | null): string | null {
    if (!errors) {
      return null;
    }

    if (errors['required'] === true) {
      return 'This field is required';
    }

    for (const error of Object.values(errors)) {
      if (typeof error !== 'object' || error === null || !('message' in error)) {
        continue;
      }

      const message = (error as { message?: unknown }).message;

      if (typeof message === 'string') {
        return message;
      }
    }

    return null;
  }

  // Constructor for the app-field component. Handles initialization and subscription to control events.
  constructor() {
    effect((onCleanup) => {
      const control = this.control();

      // Update the control state based on the current control status.
      const updateState = () => {
        this.controlSnapshot.set({
          touched: control.touched,
          invalid: control.invalid,
          value: control.value,
          errors: control.errors,
        });
      };
      // Perform an initial update to set the state based on the current control status.
      updateState();
      // Subscribe to the control's events to keep the state updated.
      const subscription = control.events.subscribe(updateState);
      // Cleanup subscription when the effect is disposed.
      onCleanup(() => subscription.unsubscribe());
    });

    // A value outside the Reject range makes the control itself invalid, so the form's own
    // validity blocks Save. Warned values still save.
    effect((onCleanup) => {
      const control = this.control();
      const limitKey = this.limitKey();

      // Read so the check runs again once the limits have loaded.
      this.referenceDataService.limits();
      if (!limitKey) return;

      const rejectCheck: ValidatorFn = (c) => {
        const value = parseNumber(c.value);
        if (value === null) return null;
        const limits = untracked(() => this.referenceDataService.limits());
        const result = this.validationService.evaluate(limitKey, value, limits);
        return result.severity === 'Reject' ? { limit: { message: result.message } } : null;
      };
      control.addValidators(rejectCheck);
      untracked(() => control.updateValueAndValidity());
      onCleanup(() => {
        control.removeValidators(rejectCheck);
        untracked(() => control.updateValueAndValidity());
      });
    });

    // Counted in the page's "1 error blocks saving · 2 warnings" summary, when the page has one.
    const issues = inject(FieldIssues, { optional: true });
    if (issues) {
      const unregister = issues.register(this.state);
      inject(DestroyRef).onDestroy(unregister);
    }
  }
}

/** "3000" → "3,000", "7.50" → "7.5". */
function formatLimitNumber(value: number): string {
  return formatNumber(value, 'en-US', '1.0-2');
}

/** "%" sits right after the number; other units get a space; "ratio" and blank show nothing. */
function withLimitUnit(text: string, unit: string): string {
  if (!unit || unit === 'ratio') return text;
  return unit === '%' ? `${text}%` : `${text} ${unit}`;
}
