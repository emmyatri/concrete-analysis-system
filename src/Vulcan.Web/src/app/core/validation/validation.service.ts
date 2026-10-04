import { Injectable } from '@angular/core';
import type { ValidationResult } from './validation-result';
import type { ValidationLimit } from '../api/api-types';

@Injectable({
  providedIn: 'root',
})
export class ValidationService {
  // Evaluates a value against a set of validation limits and returns the validation result.
  evaluate(key: string, value: number, limits: readonly ValidationLimit[]): ValidationResult {
    // Filter the validation limits to only include those that match the given key.
    const matchLimits = limits.filter((limit) => limit.key === key);

    // Find the first validation limit with severity 'Reject'.
    const rejectLimit = matchLimits.find((limit) => limit.severity === 'Reject');

    // Find the first validation limit with severity 'Warn'.
    const warnLimit = matchLimits.find((limit) => limit.severity === 'Warn');

    // Check if the value violates the 'Reject' limit first.
    if (rejectLimit && this.isOutOfBounds(value, rejectLimit)) {
      return {
        severity: rejectLimit.severity,
        message: rejectLimit.message,
      };
    }

    // Check if the value violates the 'Warn' limit if no 'Reject' violation was found.
    if (warnLimit && this.isOutOfBounds(value, warnLimit)) {
      return {
        severity: warnLimit.severity,
        message: warnLimit.message,
      };
    }
    // If no violations were found, return a result as normal.
    return { severity: null, message: null };
  }

  // Private helper method to check if a value is out of bounds
  private isOutOfBounds(value: number, limit: ValidationLimit): boolean {
    const belowMin = limit.min !== null && value < limit.min;
    const aboveMax = limit.max !== null && value > limit.max;
    return belowMin || aboveMax;
  }
}
