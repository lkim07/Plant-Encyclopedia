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

/**
 * POST /api/search/external result (API_SPEC §27). Only "found" carries a plant; plants that are
 * not published yet are reported as "pendingReview" without any plant data.
 */
export type ExternalLookupResult =
  | { readonly status: 'found'; readonly plant: SearchResultItem }
  | { readonly status: 'pendingReview' }
  | { readonly status: 'notFound' };

export type SuggestionType =
  'plant' | 'cultivar' | 'scientificName' | 'commonName' | 'synonym' | 'plantGroup';
