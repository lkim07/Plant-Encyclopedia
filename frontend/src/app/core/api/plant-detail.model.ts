/** GET /api/plants/{plantId} response data. Mirrors API_SPEC §14. */
export interface PlantDetail {
  readonly id: string;
  readonly name: string;
  readonly plantGroup: string | null;
  /** The plant group's slug; null when the plant has no group. */
  readonly plantGroupId: string | null;
  readonly scientificName: string;
  readonly description: string | null;
  /** The photo, or the illustration when there is no photo; null when the plant has no image. */
  readonly heroImage: PlantImage | null;
  /** Null unless the plant's care information is verified. */
  readonly quickCare: QuickCare | null;
  /** Credits for external sources, e.g. { type: "taxonomy", text: "Catalogue of Life, via GBIF" }. */
  readonly attributions: readonly Attribution[];
}

export interface Attribution {
  readonly type: string;
  readonly text: string;
}

export interface PlantImage {
  readonly url: string;
  readonly altText: string | null;
  readonly kind: 'photo' | 'illustration';
  /** Null only for sources without reviewed credit text. */
  readonly credit: ImageCredit | null;
}

/** What the image's licence requires us to show: author, licence and source. */
export interface ImageCredit {
  readonly author: string | null;
  /** e.g. "CC BY-SA 3.0" or "Public domain". */
  readonly license: string;
  /** The licence deed, when the version is known. */
  readonly licenseUrl: string | null;
  /** e.g. "iNaturalist" or "Wikimedia Commons". */
  readonly source: string;
  /** The image's page at the source, when known. */
  readonly sourceUrl: string | null;
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
