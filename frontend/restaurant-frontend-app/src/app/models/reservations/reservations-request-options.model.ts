/** The two tabs of the Reservations page; without a scope the API returns every reservation. */
export type ReservationsScope = 'upcoming' | 'past';

export interface ReservationsRequestOptions {
  /** Ask the API even when a recent answer could be reused. */
  fresh?: boolean;
  scope?: ReservationsScope;
}
