import { AbstractControl } from '@angular/forms';
import { Component, input } from '@angular/core';

// Reusable wrapper for a form control with a label and optional hint.
@Component({
  selector: 'app-field',
  templateUrl: './app-field.html',
  styleUrls: ['./app-field.scss'],
})

// Class representing the app-field component.
export class AppField {
  readonly label = input.required<string>();
  readonly inputId = input.required<string>();
  readonly control = input.required<AbstractControl>();
  readonly limitKey = input<string>('');
  readonly hint = input<string>('');
}
