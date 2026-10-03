import { HttpErrorResponse } from '@angular/common/http';

/** One server message for one field. `key` matches ValidationLimit.key, e.g. "Composition.CementPct". */
export interface FieldError {
  key: string;
  message: string;
}

/** What a failed call means for the screen. */
export interface ApiProblem {
  kind: 'validation' | 'unreachable' | 'server';
  /** Safe to show as-is. */
  message: string;
  /** Only for 'validation': one entry per message, in the order the server sent them. */
  fieldErrors: FieldError[];
}

export const UNREACHABLE_MESSAGE = "Can't reach the local service. Check that the app is open and try again.";
export const SERVER_ERROR_MESSAGE =
  'Something went wrong on the local service. Try again, and tell a lead if it keeps happening.';

/**
 * True when the server didn't answer at all: no connection (status 0), or the dev proxy saying the API
 * isn't running (502/503/504).
 */
export function isUnreachable(error: unknown): boolean {
  return error instanceof HttpErrorResponse && [0, 502, 503, 504].includes(error.status);
}

/**
 * Turns a failed call into something a screen can show (M1-60).
 * A 400 (ASP.NET ValidationProblemDetails) becomes a list of field errors.
 */
export function describeApiError(error: unknown): ApiProblem {
  if (isUnreachable(error)) {
    return { kind: 'unreachable', message: UNREACHABLE_MESSAGE, fieldErrors: [] };
  }

  if (error instanceof HttpErrorResponse && error.status === 400) {
    const body = error.error as { title?: string; errors?: Record<string, string[] | string> } | null;
    const fieldErrors = Object.entries(body?.errors ?? {}).flatMap(([key, messages]) =>
      (Array.isArray(messages) ? messages : [messages]).map((message) => ({ key, message })),
    );
    return { kind: 'validation', message: body?.title ?? 'Some values need fixing.', fieldErrors };
  }

  return { kind: 'server', message: SERVER_ERROR_MESSAGE, fieldErrors: [] };
}

/**
 * Splits field errors into the ones a screen has a field for (shown under that field) and the rest
 * (shown in a banner at the top), as M1-45 step 6 asks.
 */
export function matchFieldErrors(
  fieldErrors: FieldError[],
  fieldKeys: Iterable<string>,
): { byKey: Map<string, string[]>; unmatched: string[] } {
  const known = new Set(fieldKeys);
  const byKey = new Map<string, string[]>();
  const unmatched: string[] = [];

  for (const { key, message } of fieldErrors) {
    if (known.has(key)) byKey.set(key, [...(byKey.get(key) ?? []), message]);
    else unmatched.push(message);
  }
  return { byKey, unmatched };
}
