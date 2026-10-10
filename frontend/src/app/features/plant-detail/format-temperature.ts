import { TemperatureCare } from '../../core/api/plant-detail.model';

/** Display text for the Temp card: "15–25°C", "From 15°C", "Up to 25°C", or null if there is no value. */
export function formatTemperature({ minC, maxC }: TemperatureCare): string | null {
  if (minC !== null && maxC !== null) {
    return `${minC}–${maxC}°C`;
  }
  if (minC !== null) {
    return `From ${minC}°C`;
  }
  if (maxC !== null) {
    return `Up to ${maxC}°C`;
  }
  return null;
}
