/** Browsing response data. Mirrors API_SPEC §17–19. IDs are slugs except plant IDs (UUIDs). */

/** GET /api/categories item. */
export interface CategorySummary {
  readonly id: string;
  readonly name: string;
  /** Always null until category images exist. */
  readonly imageUrl: string | null;
}

/** GET /api/categories/{categoryId} data. */
export interface CategoryDetail {
  readonly id: string;
  readonly name: string;
  readonly plantGroups: readonly PlantGroupCard[];
}

export interface PlantGroupCard {
  readonly id: string;
  readonly name: string;
  readonly imageUrl: string | null;
}

/** GET /api/plant-groups/{plantGroupId} data. */
export interface PlantGroupDetail {
  readonly id: string;
  readonly name: string;
  readonly category: CategoryReference;
  readonly plants: readonly PlantCard[];
}

export interface CategoryReference {
  readonly id: string;
  readonly name: string;
}

export interface PlantCard {
  readonly id: string;
  readonly name: string;
  readonly imageUrl: string | null;
}
