import { ValidatorFn } from '@angular/forms';

import { parseNumber } from './parse-number';

/**
 * A typed number. Blank (or only spaces) counts as missing; text that isn't a number is an error.
 * `maxDecimals` 0 means whole numbers only (schema.md: ExpectedFinalBreakPsi is an int, FinalWeightLb max 2 decimals).
 * The range checks come from ValidationLimit through AppField, not from here.
 */
export function numberField(maxDecimals?: number): ValidatorFn {
  return (control) => {
    const text = String(control.value ?? '').trim();
    if (!text) return { required: true };
    if (parseNumber(text) === null) return { number: { message: 'Enter a number' } };
    if (maxDecimals !== undefined && decimalPlaces(text) > maxDecimals) {
      const message =
        maxDecimals === 0 ? 'Whole numbers only' : `Max ${maxDecimals} decimal places`;
      return { decimals: { message } };
    }
    return null;
  };
}

function decimalPlaces(text: string): number {
  const dot = text.indexOf('.');
  return dot === -1 ? 0 : text.length - dot - 1;
}
