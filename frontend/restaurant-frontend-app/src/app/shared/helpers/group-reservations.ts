import { Reservation, ReservationStatus } from '@app/models/reservations/reservation.model';

/**
 * The reservation whose group cart the header Cart opens when the page doesn't name one:
 * the nearest upcoming "Reserved" reservation with 2+ guests (only those have a group pre-order).
 * A group still collecting pre-orders wins over a nearer one already sent to the restaurant.
 */
export function findNearestGroupReservationId(reservations: Reservation[], now: Date = new Date()): string | null {
  const upcoming = reservations
    .filter(
      (reservation) =>
        reservation.status === ReservationStatus.Reserved &&
        reservation.guestCount >= 2 &&
        new Date(reservation.endTime).getTime() > now.getTime(),
    )
    .sort((first, second) => new Date(first.startTime).getTime() - new Date(second.startTime).getTime());
  const open = upcoming.find((reservation) => (reservation.groupPreOrder?.status ?? 'Open') === 'Open');
  return (open ?? upcoming[0])?.id ?? null;
}
