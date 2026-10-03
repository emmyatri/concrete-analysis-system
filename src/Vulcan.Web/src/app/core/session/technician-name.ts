import { ValidatorFn } from '@angular/forms';

/** Same rules as Vulcan.Domain TechnicianName. The API checks again on save (NFR-6). */
export const TECHNICIAN_NAME_MIN = 2;
export const TECHNICIAN_NAME_MAX = 50;

/** The message to show under the field, or null when the name is fine. Checks the trimmed name. */
export function technicianNameError(value: unknown): string | null {
  const trimmed = String(value ?? '').trim();
  if (!trimmed) return 'Enter your name';
  if (trimmed.length < TECHNICIAN_NAME_MIN) return `At least ${TECHNICIAN_NAME_MIN} characters`;
  if (trimmed.length > TECHNICIAN_NAME_MAX) return `${TECHNICIAN_NAME_MAX} characters max`;
  return null;
}

export const technicianNameField: ValidatorFn = (control) => {
  const message = technicianNameError(control.value);
  return message ? { technicianName: { message } } : null;
};
