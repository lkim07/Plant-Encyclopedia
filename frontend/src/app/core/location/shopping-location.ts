import { Injectable, signal } from '@angular/core';

/** Where to look for shops. */
export interface ShoppingLocation {
  readonly label: string;
  readonly latitude: number;
  readonly longitude: number;
  /** device: the browser's location; default: Vancouver, BC; place: chosen by hand. */
  readonly source: 'device' | 'default' | 'place';
}

/** The MVP's default location (D-035): downtown Vancouver, BC. */
export const DEFAULT_LOCATION: ShoppingLocation = {
  label: 'Vancouver, BC',
  latitude: 49.2827,
  longitude: -123.1207,
  source: 'default',
};

/** How long to wait for the device's location before using the default. */
export const LOCATION_TIMEOUT_MS = 10_000;

/**
 * The location for Where to Buy (PRD §28, D-035). The browser is asked only when Where to Buy needs a location, and
 * only once per visit; if it is denied, unavailable or slow, Vancouver, BC is used and the user can choose a town.
 * The location stays in memory for this visit: it is never saved in the browser or sent anywhere but the shop
 * search, and the device position is rounded to about 100 m first.
 */
@Injectable({ providedIn: 'root' })
export class ShoppingLocationService {
  private readonly current = signal<ShoppingLocation | null>(null);
  private request?: Promise<ShoppingLocation>;

  /** The chosen location, or null before Where to Buy has asked for one. */
  readonly location = this.current.asReadonly();
  /** Set when the device location could not be used, so the page can say why it shows Vancouver. */
  readonly fellBack = signal(false);

  /** The current location, asking the browser the first time. */
  resolve(): Promise<ShoppingLocation> {
    const known = this.current();
    if (known) {
      return Promise.resolve(known);
    }
    this.request ??= this.ask().then((location) => {
      this.current.set(location);
      return location;
    });
    return this.request;
  }

  /** A location chosen by hand (or the device location again via `useDevice`). */
  choose(location: ShoppingLocation): void {
    this.current.set(location);
    this.fellBack.set(false);
  }

  /** Asks the browser again, e.g. after the user allowed location in their settings. */
  async useDevice(): Promise<ShoppingLocation> {
    this.request = undefined;
    this.current.set(null);
    return this.resolve();
  }

  private ask(): Promise<ShoppingLocation> {
    return new Promise((resolve) => {
      const fallback = () => {
        this.fellBack.set(true);
        resolve(DEFAULT_LOCATION);
      };
      if (typeof navigator === 'undefined' || !navigator.geolocation) {
        fallback();
        return;
      }
      navigator.geolocation.getCurrentPosition(
        (position) => {
          this.fellBack.set(false);
          resolve({
            label: 'Your location',
            latitude: round(position.coords.latitude),
            longitude: round(position.coords.longitude),
            source: 'device',
          });
        },
        fallback,
        { enableHighAccuracy: false, timeout: LOCATION_TIMEOUT_MS, maximumAge: 5 * 60 * 1000 },
      );
    });
  }
}

/** Three decimals, about 100 m: enough to sort shops by distance, without a precise position. */
function round(value: number): number {
  return Math.round(value * 1000) / 1000;
}
