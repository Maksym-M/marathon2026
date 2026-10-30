import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';

const startOf = (reservation: ReservationCustomer) => new Date(reservation.startTime).getTime();

/** "Upcoming" tab: Reserved or In Progress — the same split as `GET /reservations?scope=upcoming`. */
export const isUpcomingReservation = (reservation: ReservationCustomer): boolean =>
  reservation.status === ReservationStatusCustomer.Reserved || reservation.status === ReservationStatusCustomer.InProgress;

/**
 * US20 AC2–AC3: "Upcoming" — Reserved or In Progress, nearest first;
 * "Past & Cancelled" — Completed or Cancelled, most recent first.
 */
export function splitReservationsByTab(reservations: ReservationCustomer[]): {
  upcoming: ReservationCustomer[];
  past: ReservationCustomer[];
} {
  return {
    upcoming: reservations.filter(isUpcomingReservation).sort((first, second) => startOf(first) - startOf(second)),
    past: reservations
      .filter((reservation) => !isUpcomingReservation(reservation))
      .sort((first, second) => startOf(second) - startOf(first)),
  };
}
