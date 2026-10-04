import type { ValidationSeverity } from '../api/models';

// Type representing the result of a validation check.
export type ValidationResult =
  // No issues found.
  | { severity: null; message: null }
  // Validation issue found.
  | { severity: ValidationSeverity; message: string };
