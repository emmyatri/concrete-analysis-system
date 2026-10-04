import { AbstractControl, ValidationErrors } from '@angular/forms';
import { Component, input, signal, effect, computed, inject } from '@angular/core';
import { ValidationService } from '../../core/validation/validation.service';
import type { ValidationLimit } from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';

// Possible states for the app-field component.
export type AppFieldState = 'error' | 'warn' | 'normal';

// Keys for which the maximum value should be displayed in the hint.
const KEYS_WITH_DISPLAYED_MAXIMUM = new Set(['Oxide.MagnesiumOxidePct', 'Oxide.SulfurTrioxidePct']);

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
    value: null,
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
    const rawValue = this.controlSnapshot().value;
    const limits = this.referenceDataService.limits();
    const limitKey = this.limitKey();
    if (!limitKey || typeof rawValue !== 'number' || !Number.isFinite(rawValue)) {
      return { severity: null, message: null };
    }
    return this.validationService.evaluate(limitKey, rawValue, limits);
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

  // Formats the minimum, maximum, and unit for the range hint.
  private formatLimitHint(
    normalLimit: ValidationLimit,
    maximumLimit?: ValidationLimit,
  ): string | null {
    const maximumText =
      maximumLimit && maximumLimit.max !== null ? ` · max ${maximumLimit.max}` : '';
    // Conditional formatting of the limit.
    if (normalLimit.min !== null && normalLimit.max !== null) {
      const { min, max, unit } = normalLimit;
      return `${min}-${max}${maximumText}` + (unit ? ` ${unit}` : '');
    } else if (normalLimit.min !== null) {
      const { min, unit } = normalLimit;
      return `≥ ${min}` + (unit ? ` ${unit}` : '');
    } else if (normalLimit.max !== null) {
      const { max, unit } = normalLimit;
      return `≤ ${max}` + (unit ? ` ${unit}` : '');
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
  }
}
