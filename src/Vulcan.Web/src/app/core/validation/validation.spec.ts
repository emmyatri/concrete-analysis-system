import { TestBed } from '@angular/core/testing';
import { ValidationService } from './validation.service';
import type { ValidationLimit } from '../api/models';

// Unit tests for the ValidationService
describe('ValidationService', () => {
  let service: ValidationService;

  // Small scoped mock validation limits for testing purposes.
  const limits: ValidationLimit[] = [
    {
      key: 'Batch.ExpectedFinalBreakPsi',
      severity: 'Reject',
      min: 3000,
      max: 5000,
      unit: 'PSI',
      message: 'PSI must be within the range [3,000-5,000]',
      source: 'unit test fixture',
    },
    {
      key: 'Batch.MixWaterPh',
      severity: 'Reject',
      min: 1,
      max: 9,
      unit: '',
      message: 'pH must be within the range [1-9]',
      source: 'unit test fixture',
    },
    {
      key: 'Batch.MixWaterPh',
      severity: 'Warn',
      min: 7,
      max: 7.5,
      unit: '',
      message: 'Warning: pH is outside the normal range [7.0-7.5]',
      source: 'unit test fixture',
    },
  ];

  // Setup the testing module and inject the ValidationService before each test.
  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ValidationService);
  });

  // Should create the service successfully.
  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  // Test for normal values
  it('should evaluate normal values correctly', () => {
    const result = service.evaluate('Batch.ExpectedFinalBreakPsi', 3500, limits);
    expect(result).toEqual({ severity: null, message: null });
  });

  // Test for reject values
  it('should evaluate reject values correctly', () => {
    const result = service.evaluate('Batch.MixWaterPh', 0, limits);
    expect(result).toEqual({
      severity: 'Reject',
      message: 'pH must be within the range [1-9]',
    });
  });

  // Test for warn values
  it('should evaluate warn values correctly', () => {
    const result = service.evaluate('Batch.MixWaterPh', 6, limits);
    expect(result).toEqual({
      severity: 'Warn',
      message: 'Warning: pH is outside the normal range [7.0-7.5]',
    });
  });
});
