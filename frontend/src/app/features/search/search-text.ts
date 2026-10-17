/** Mirrors the API's search-text limits (API_SPEC §24) so invalid text is caught before a request. */
export const MAX_SEARCH_LENGTH = 100;
const MAX_WORDS = 10;

// One word of a scientific name: letters, optionally with "." or "-" ("var.", "bursa-pastoris"), or "×".
const NAME_WORD = /^(?:×|\p{L}[\p{L}\p{M}.\-]*)$/u;

/**
 * Whether the text could be a scientific name worth looking up in Catalogue of Life: 2–4 words of letters,
 * starting with a letter. Mirrors the API's check (API_SPEC §27), which has the final say.
 */
export function isNameShaped(text: string): boolean {
  const words = text.trim().split(/\s+/u);
  return (
    words.length >= 2 &&
    words.length <= 4 &&
    /^\p{L}/u.test(words[0]) &&
    words.every((word) => NAME_WORD.test(word))
  );
}

/** Returns a message for the user when the trimmed text cannot be searched, otherwise null. */
export function validateSearchText(text: string): string | null {
  const value = text.trim();
  if (!value) {
    return 'Enter a plant name to search.';
  }
  if (value.length > MAX_SEARCH_LENGTH) {
    return `Use at most ${MAX_SEARCH_LENGTH} characters.`;
  }
  if (/\p{Cc}/u.test(value)) {
    return 'Remove special control characters.';
  }
  if (!/[\p{L}\p{N}]/u.test(value)) {
    return 'Enter at least one letter or number.';
  }
  if ((value.match(/[\p{L}\p{N}\p{M}]+/gu) ?? []).length > MAX_WORDS) {
    return `Use at most ${MAX_WORDS} words.`;
  }
  return null;
}
