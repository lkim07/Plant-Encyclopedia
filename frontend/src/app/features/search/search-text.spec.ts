import { isNameShaped } from './search-text';

describe('isNameShaped', () => {
  // Protects against: offering the Catalogue of Life lookup for text the API would not send (API_SPEC §27).
  it.each([
    ['Rosa canina', true],
    ['rosa canina', true],
    ['Mentha × piperita', true],
    ['Brassica oleracea var. capitata', true],
    ['Capsella bursa-pastoris', true],
    ['rose', false],
    ['a b c d e', false],
    ['rose 2024', false],
    ["Rosa 'Peace'", false],
    ['× Citrofortunella microcarpa', false],
  ])('%s → %s', (text, expected) => {
    expect(isNameShaped(text)).toBe(expected);
  });
});
