import { parseNumber } from './parse-number';
// Test cases for the parseNumber function.
describe('parseNumber', () => {
  // Test parsing numbers with commas.
  it('parses numbers containing commas', () => {
    expect(parseNumber('4,380')).toBe(4380);
  });

  // Test parsing valid numbers without commas.
  it('parses valid numbers without commas', () => {
    expect(parseNumber('4380')).toBe(4380);
    expect(parseNumber(' 4380 ')).toBe(4380);
    expect(parseNumber('-4380')).toBe(-4380);
    expect(parseNumber('.438')).toBe(0.438);
    expect(parseNumber('-.438')).toBe(-0.438);
  });

  // Test parsing invalid numbers.
  it('returns null for invalid numbers', () => {
    expect(parseNumber('abc')).toBeNull();
    expect(parseNumber('')).toBeNull();
    expect(parseNumber(null)).toBeNull();
    expect(parseNumber(undefined)).toBeNull();
  });

  // Test parsing non-finite numbers.
  it('returns null for non-finite numbers', () => {
    expect(parseNumber(Infinity)).toBeNull();
    expect(parseNumber(-Infinity)).toBeNull();
    expect(parseNumber(NaN)).toBeNull();
  });
});
