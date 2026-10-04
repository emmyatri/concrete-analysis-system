import { AbstractControl } from '@angular/forms';
import { Component, input, signal, effect, computed, inject } from '@angular/core';
import { ValidationService } from '../../core/validation/validation.service';
import type { ValidationLimit } from '../../core/api/models';
import { ReferenceDataService } from '../../core/api/reference-data.service';

// Possible states for the app-field component.
export type AppFieldState = 'error' | 'warn' | 'normal';

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
  });

  // Computed state of the app-field based on the control snapshot.
  readonly state = computed<AppFieldState>(() => {
    const { touched, invalid } = this.controlSnapshot();
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

  // Computed message based on the current validation result.
  readonly message = computed(() => {
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

    // Select the most severe validation limit available, prioritizing 'Reject' over 'Warn'.
    const selectedLimit = rejectLimit ?? warnLimit;

    // Return the appropriate hint message based on the validation limits - start with the most
    // severe (Reject) and then Warn.
    if (!selectedLimit) {
      return hint;
    }
    return this.formatLimitHint(selectedLimit) ?? hint;
  });

  // Formats the minimum, maximum, and unit for the range hint.
  private formatLimitHint(limit: ValidationLimit): string | null {
    // Conditional formatting of the limit.
    if (limit.min !== null && limit.max !== null) {
      const { min, max, unit } = limit;
      return `${min}-${max}` + (unit ? ` ${unit}` : '');
    } else if (limit.min !== null) {
      const { min, unit } = limit;
      return `≥ ${min}` + (unit ? ` ${unit}` : '');
    } else if (limit.max !== null) {
      const { max, unit } = limit;
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
