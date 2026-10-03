import { AbstractControl } from '@angular/forms';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-field',
  templateUrl: './app-field.html',
  styleUrl: './app-field.scss',
})
export class AppField {
  @Input() label!: string;
  @Input() inputId!: string;
  @Input() control!: AbstractControl;
  @Input() limitKey!: string;
  @Input() hint = '';
}
