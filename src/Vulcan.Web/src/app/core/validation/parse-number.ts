// Parses a string or number into a finite number, returning null if invalid.

export function parseNumber(text: unknown): number | null {
  if (typeof text === 'number') return Number.isFinite(text) ? text : null;
  if (typeof text !== 'string') return null;
  const cleaned = text.trim().replace(/,/g, '');
  if (!/^-?\d+(\.\d+)?$|^-?\.\d+$/.test(cleaned)) return null;
  const value = Number(cleaned);
  return Number.isFinite(value) ? value : null;
}
