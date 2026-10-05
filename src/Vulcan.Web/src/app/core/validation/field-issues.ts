import { Injectable, Signal, computed, signal } from '@angular/core';

import type { AppFieldState } from '../../shared/app-field/app-field';

/**
 * Counts the red and amber fields on one page, for the summary next to Save
 * (1 error blocks saving · 2 warnings). Provide it on the page component;
 * every AppField inside registers itself.
 */
@Injectable()
export class FieldIssues {
  private readonly states = signal<Signal<AppFieldState>[]>([]);

  readonly errors = computed(() => this.states().filter((state) => state() === 'error').length);
  readonly warnings = computed(() => this.states().filter((state) => state() === 'warn').length);

  /** "1 error blocks saving · 2 warnings", or "" when every field is fine. */
  readonly summary = computed(() => describeIssues(this.errors(), this.warnings()));

  /** Adds one field. Returns the function that removes it again. */
  register(state: Signal<AppFieldState>): () => void {
    this.states.update((states) => [...states, state]);
    return () => this.states.update((states) => states.filter((s) => s !== state));
  }
}

export function describeIssues(errors: number, warnings: number): string {
  const parts: string[] = [];
  if (errors > 0) parts.push(`${errors} ${errors === 1 ? 'error blocks' : 'errors block'} saving`);
  if (warnings > 0) parts.push(`${warnings} ${warnings === 1 ? 'warning' : 'warnings'}`);
  return parts.join(' · ');
}
