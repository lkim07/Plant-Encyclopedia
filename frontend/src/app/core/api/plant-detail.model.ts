/** GET /api/plants/{plantId} response data. Mirrors API_SPEC §14. */
export interface PlantDetail {
  readonly id: string;
  readonly name: string;
  readonly plantGroup: string | null;
  readonly scientificName: string;
  readonly description: string | null;
  /** Always null until the hero-image approach is decided. */
  readonly heroImage: HeroImage | null;
  /** Null unless the plant's care information is verified. */
  readonly quickCare: QuickCare | null;
}

export interface HeroImage {
  readonly url: string;
  readonly altText: string | null;
}

/** A card is null when it has no value. */
export interface QuickCare {
  readonly light: CareCard | null;
  readonly water: CareCard | null;
  readonly temperature: TemperatureCare | null;
  readonly soil: CareCard | null;
}

export interface CareCard {
  readonly value: string;
  readonly description: string | null;
}

/** Degrees Celsius; the UI formats the display text. */
export interface TemperatureCare {
  readonly minC: number | null;
  readonly maxC: number | null;
  readonly description: string | null;
}
