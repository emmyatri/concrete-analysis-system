import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppField } from './app-field';
import { FormControl } from '@angular/forms';
import { ReferenceDataService } from '../../core/api/reference-data.service';
import { MOCK_VALIDATION_LIMITS } from '../../core/api/mock/mock-data';
import { signal } from '@angular/core';

// Unit tests for the AppField component.
describe('AppField', () => {
  let fixture: ComponentFixture<AppField>;
  let component: AppField;
  let control: FormControl<number | string | null>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AppField],
      providers: [
        { provide: ReferenceDataService, useValue: { limits: signal(MOCK_VALIDATION_LIMITS) } },
      ],
    });

    // Create a new FormControl with an initial value of 3500.
    control = new FormControl<number | string | null>(null);
    // Create the test component for the AppField component.
    fixture = TestBed.createComponent(AppField);
    // Assign input properties to the test component.
    fixture.componentRef.setInput('label', 'Mix Water pH');
    fixture.componentRef.setInput('control', control);
    fixture.componentRef.setInput('inputId', 'mixWaterPh');
    fixture.componentRef.setInput('limitKey', 'Batch.MixWaterPh');

    // Trigger change detection to apply the input properties.
    fixture.detectChanges();
    component = fixture.componentInstance;
  });

  // Check if the component is created successfully.
  it('should create', () => {
    expect(component).toBeTruthy();
  });

  // Test normal state
  it('should return normal state', () => {
    expect(component.state()).toBe('normal');
    expect(component.message()).toBe('');
  });

  // Test string input value
  it('should return error state for string input', () => {
    control.setValue('0');
    control.markAsTouched();
    fixture.detectChanges();

    expect(component.state()).toBe('error');
    expect(component.message()).toBe('pH must be within the range [1-9]');
  });

  // Test error state
  it('should return error state', () => {
    control.setValue(0);
    control.markAsTouched();
    fixture.detectChanges();

    expect(component.state()).toBe('error');
    expect(component.message()).toBe('pH must be within the range [1-9]');
  });

  // Test warning state
  it('should return warning state', () => {
    control.setValue(6);
    control.markAsTouched();
    fixture.detectChanges();

    expect(component.state()).toBe('warn');
    expect(component.message()).toBe('pH is outside the normal range [7.0-7.5]');
  });

  // Test for reject hint
  it('returns the reject range hint when outside the allowed range', () => {
    control.setValue(0);
    control.markAsTouched();
    fixture.detectChanges();
    expect(component.computedHint()).toBe('1-9');
  });

  // Test for normal/warn hint
  it('uses the normal warn range for the hint when both limits exist', () => {
    expect(component.computedHint()).toBe('7-7.5');
  });

  // Test server error state even when the control has not been touched.
  it('should display a server error even when untouched', () => {
    const serverMessage = 'Batch number already exists';

    control.setErrors({ server: serverMessage });
    fixture.detectChanges();

    expect(component.state()).toBe('error');
    expect(component.message()).toBe(serverMessage);
  });

  // Test MgO range/max hint
  it('should display the range and the maximum hint when applicable', () => {
    fixture.componentRef.setInput('limitKey', 'Oxide.MagnesiumOxidePct');
    fixture.detectChanges();
    expect(component.computedHint()).toBe('0.1-4 · max 6 %');
  });

  // Test SO₃ range/max hint
  it('should display the range and the maximum hint when applicable', () => {
    fixture.componentRef.setInput('limitKey', 'Oxide.SulfurTrioxidePct');
    fixture.detectChanges();
    expect(component.computedHint()).toBe('1.3-3 · max 4.5 %');
  });

  // Test for custom local validation message
  it('should display a custom local validation message', () => {
    control.setErrors({
      batchNumber: {
        message: 'Letters, digits and dashes only',
      },
    });
    control.markAsTouched();
    fixture.detectChanges();

    expect(component.state()).toBe('error');
    expect(component.message()).toBe('Letters, digits and dashes only');
  });
});
