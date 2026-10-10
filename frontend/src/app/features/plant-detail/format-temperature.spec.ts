import { formatTemperature } from './format-temperature';

describe('formatTemperature', () => {
  it('formats a full range', () => {
    expect(formatTemperature({ minC: 15, maxC: 25, description: null })).toBe('15–25°C');
  });

  it('formats a minimum only', () => {
    expect(formatTemperature({ minC: 15, maxC: null, description: null })).toBe('From 15°C');
  });

  it('formats a maximum only', () => {
    expect(formatTemperature({ minC: null, maxC: 25, description: null })).toBe('Up to 25°C');
  });

  it('keeps decimals and negative values', () => {
    expect(formatTemperature({ minC: -5.5, maxC: 10, description: null })).toBe('-5.5–10°C');
  });

  it('returns null when there is no value', () => {
    expect(formatTemperature({ minC: null, maxC: null, description: 'x' })).toBeNull();
  });
});
