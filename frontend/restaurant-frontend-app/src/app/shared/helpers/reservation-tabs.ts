import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';

const startOf = (reservation: ReservationCustomer) => new Date(reservation.startTime).getTime();

/**
 * US20 AC2–AC3: "Upcoming" — Reserved or In Progress, nearest first;
 * "Past & Cancelled" — Completed or Cancelled, most recent first.
 */
export function splitReservationsByTab(reservations: ReservationCustomer[]): {
  upcoming: ReservationCustomer[];
  past: ReservationCustomer[];
} {
  const isUpcoming = (reservation: ReservationCustomer) =>
    reservation.status === ReservationStatusCustomer.Reserved || reservation.status === ReservationStatusCustomer.InProgress;

  return {
    upcoming: reservations.filter(isUpcoming).sort((first, second) => startOf(first) - startOf(second)),
    past: reservations.filter((reservation) => !isUpcoming(reservation)).sort((first, second) => startOf(second) - startOf(first)),
  };
}
