/**
 * Room to the right of the strength chart's plot for its end labels ("28-day est. 5,028 (−10%)").
 * The curing strip uses the same padding so the two x-axes stay lined up for the shared hover line.
 */
export function rightGutter(fontSize = 12): number {
  return Math.round((150 * fontSize) / 12);
}
