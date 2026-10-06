import { dateGroup } from './date-group';

describe('dateGroup', () => {
  // Wednesday, October 14, 2026, 3:00 PM local time.
  const now = new Date(2026, 9, 14, 15, 0);
  const on = (month: number, day: number, year = 2026) => new Date(year, month - 1, day, 9, 0);

  it.each([
    { when: 'earlier today', date: on(10, 14), expected: 'Today' },
    { when: 'later today', date: new Date(2026, 9, 14, 23, 59), expected: 'Today' },
    { when: 'tomorrow', date: on(10, 15), expected: 'Today' },
    { when: 'yesterday', date: on(10, 13), expected: 'Yesterday' },
    { when: 'Sunday, the start of this week', date: on(10, 11), expected: 'Earlier this week' },
    { when: 'Saturday, the end of last week', date: on(10, 10), expected: 'Last week' },
    { when: 'Sunday, the start of last week', date: on(10, 4), expected: 'Last week' },
    { when: 'the day before last week', date: on(10, 3), expected: 'Earlier this month' },
    { when: 'the 1st of this month', date: on(10, 1), expected: 'Earlier this month' },
    { when: 'the last day of last month', date: on(9, 30), expected: 'Last month' },
    { when: 'the 1st of last month', date: on(9, 1), expected: 'Last month' },
    { when: 'earlier this year', date: on(1, 1), expected: 'Earlier this year' },
    { when: 'last year', date: on(12, 31, 2025), expected: 'Older' },
  ])('$when is "$expected"', ({ date, expected }) => {
    expect(dateGroup(date, now)).toBe(expected);
  });

  it('calls Sunday "Yesterday" on a Monday, not "Earlier this week"', () => {
    expect(dateGroup(on(10, 11), new Date(2026, 9, 12, 8, 0))).toBe('Yesterday');
  });

  it('puts the last days of last month under "Last week" when they were last week', () => {
    // Monday, November 2: last week ran Sunday, October 25, to Saturday, October 31.
    expect(dateGroup(on(10, 30), new Date(2026, 10, 2, 8, 0))).toBe('Last week');
  });

  it('handles January: last month is December of last year', () => {
    expect(dateGroup(on(12, 10, 2025), new Date(2026, 0, 20, 8, 0))).toBe('Last month');
  });
});
