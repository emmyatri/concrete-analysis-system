import { HttpErrorResponse } from '@angular/common/http';

import { UNREACHABLE_MESSAGE, describeApiError, matchFieldErrors } from './api-errors';

describe('describeApiError', () => {
  it('turns a 400 into the right field errors', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: {
        title: 'One or more validation errors occurred.',
        errors: {
          'Composition.CementPct': ['Cement composition must be within the range [10-15]'],
          'Batch.BatchNumber': ['Batch number VM-2026-0142 already exists'],
        },
      },
    });

    const problem = describeApiError(error);

    expect(problem.kind).toBe('validation');
    expect(problem.fieldErrors).toEqual([
      { key: 'Composition.CementPct', message: 'Cement composition must be within the range [10-15]' },
      { key: 'Batch.BatchNumber', message: 'Batch number VM-2026-0142 already exists' },
    ]);
  });

  it('shows the "can\'t reach the server" message when there is no answer', () => {
    expect(describeApiError(new HttpErrorResponse({ status: 0 })).message).toBe(UNREACHABLE_MESSAGE);
    // The dev proxy answers 504 when the local API isn't running.
    expect(describeApiError(new HttpErrorResponse({ status: 504 })).kind).toBe('unreachable');
  });

  it('gives a general message for other server failures', () => {
    expect(describeApiError(new HttpErrorResponse({ status: 500 })).kind).toBe('server');
  });
});

describe('matchFieldErrors', () => {
  it('puts errors under their field and the rest in the banner list', () => {
    const { byKey, unmatched } = matchFieldErrors(
      [
        { key: 'Composition.CementPct', message: 'Too high' },
        { key: 'Technician', message: 'Enter your name' },
      ],
      ['Composition.CementPct', 'Composition.WaterPct'],
    );

    expect(byKey.get('Composition.CementPct')).toEqual(['Too high']);
    expect(unmatched).toEqual(['Enter your name']);
  });
});
