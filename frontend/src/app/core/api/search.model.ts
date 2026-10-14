/** GET /api/search result item. Mirrors API_SPEC §25. */
export interface SearchResultItem {
  readonly id: string;
  readonly name: string;
  readonly plantGroup: string | null;
  readonly scientificName: string;
  /** Always null until plant images are served. */
  readonly imageUrl: string | null;
}

/** GET /api/search/suggestions item. Mirrors API_SPEC §26. Selecting one runs a search for `text`. */
export interface SearchSuggestion {
  readonly text: string;
  readonly type: SuggestionType;
}

export type SuggestionType =
  'plant' | 'cultivar' | 'scientificName' | 'commonName' | 'synonym' | 'plantGroup';
