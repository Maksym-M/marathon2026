import { GroupReservationStatus } from '../group-orders/group-status.model';

/** The group pre-order of a reservation as the "Reservations" list shows it (US20 AC6–AC7). */
export interface GroupPreOrderSummary {
  status: GroupReservationStatus;
  /** Participants with a submitted pre-order; the Owner counts once they have dishes. */
  submitted: number;
  /** The Owner plus every guest who joined. */
  joined: number;
  /** The guests count, or the number of participants if more have joined. */
  seats: number;
}
