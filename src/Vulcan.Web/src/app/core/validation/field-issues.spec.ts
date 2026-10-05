import { signal } from '@angular/core';

import type { AppFieldState } from '../../shared/app-field/app-field';
import { FieldIssues, describeIssues } from './field-issues';

describe('FieldIssues', () => {
  it('words the summary like the mockup', () => {
    expect(describeIssues(0, 0)).toBe('');
    expect(describeIssues(1, 2)).toBe('1 error blocks saving · 2 warnings');
    expect(describeIssues(2, 0)).toBe('2 errors block saving');
    expect(describeIssues(0, 1)).toBe('1 warning');
  });

  it('counts the red and amber fields, and stops counting a removed field', () => {
    const issues = new FieldIssues();
    const first = signal<AppFieldState>('error');
    const second = signal<AppFieldState>('warn');
    issues.register(first);
    const removeSecond = issues.register(second);

    expect(issues.summary()).toBe('1 error blocks saving · 1 warning');

    first.set('normal');
    removeSecond();
    expect(issues.errors()).toBe(0);
    expect(issues.warnings()).toBe(0);
  });
});
