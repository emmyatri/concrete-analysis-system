import { AbstractControl } from '@angular/forms';
import { Component, input, signal, effect, computed } from '@angular/core';

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
  // Snapshot of the control's current state.
  private readonly controlSnapshot = signal({
    // User has not interacted with the control yet.
    touched: false,
    // Control currently has no rejected validation error.
    invalid: false,
  });

  // Computed state of the app-field based on the control snapshot.
  readonly state = computed<AppFieldState>(() => {
    const { touched, invalid } = this.controlSnapshot();
    if (touched && invalid) {
      return 'error';
    }
    // TODO: Add additional state for 'warn'.
    return 'normal';
  });

  readonly label = input.required<string>();
  readonly inputId = input.required<string>();
  readonly control = input.required<AbstractControl>();
  readonly limitKey = input<string>('');
  readonly hint = input<string>('');

  constructor() {
    effect((onCleanup) => {
      const control = this.control();
      // Update the control state based on the current control status.
      const updateState = () => {
        this.controlSnapshot.set({
          touched: control.touched,
          invalid: control.invalid,
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
