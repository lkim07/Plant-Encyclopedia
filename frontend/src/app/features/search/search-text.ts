/** Mirrors the API's search-text limits (API_SPEC §24) so invalid text is caught before a request. */
export const MAX_SEARCH_LENGTH = 100;
const MAX_WORDS = 10;

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
