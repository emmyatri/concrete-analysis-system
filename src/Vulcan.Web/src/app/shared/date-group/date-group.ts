/** Headings for lists grouped by date, newest first, like Windows File Explorer. */
export const DATE_GROUPS = [
  'Today',
  'Yesterday',
  'Earlier this week',
  'Last week',
  'Earlier this month',
  'Last month',
  'Earlier this year',
  'Older',
] as const;

export type DateGroup = (typeof DATE_GROUPS)[number];

/**
 * Which heading a date falls under, in local time. Weeks start on Sunday. A date that fits more than
 * one heading gets the first one (a Monday's "Yesterday" is not also "Earlier this week").
 * Dates later than today count as Today.
 */
export function dateGroup(date: Date, now = new Date()): DateGroup {
  const year = now.getFullYear();
  const month = now.getMonth();
  const day = now.getDate();
  const daysAgo = (days: number) => new Date(year, month, day - days);

  const starts: Date[] = [
    daysAgo(0), // Today
    daysAgo(1), // Yesterday
    daysAgo(now.getDay()), // Earlier this week (since Sunday)
    daysAgo(now.getDay() + 7), // Last week
    new Date(year, month, 1), // Earlier this month
    new Date(year, month - 1, 1), // Last month
    new Date(year, 0, 1), // Earlier this year
  ];
  const index = starts.findIndex((start) => date >= start);
  return DATE_GROUPS[index === -1 ? DATE_GROUPS.length - 1 : index];
}
