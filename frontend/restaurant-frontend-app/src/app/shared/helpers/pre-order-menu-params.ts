import { Params } from '@angular/router';
import { formatTime } from './date-helpers';

export interface PreOrderMenuReservation {
  id: string;
  tableId: string;
  startTime: Date | string;
  endTime: Date | string;
  guestCount: number;
  locationAddress: string;
  tableName?: string;
  tableNumber?: number | null;
}

/** Query params that open `/menu` in pre-order mode for a reservation (read by the menu page). */
export function buildPreOrderMenuParams(reservation: PreOrderMenuReservation): Params {
  const startTime = new Date(reservation.startTime);
  const endTime = new Date(reservation.endTime);
  const tableLabel = reservation.tableName || `Table ${reservation.tableNumber ?? reservation.tableId}`;

  return {
    preOrder: 'true',
    reservationId: reservation.id,
    // 2+ guests have a group pre-order: the header Cart opens the group cart for it.
    group: reservation.guestCount >= 2 ? 'true' : null,
    reservationLabel: [
      reservation.locationAddress,
      tableLabel,
      startTime.toDateString(),
      `${formatTime(startTime.toISOString())} - ${formatTime(endTime.toISOString())}`,
    ].join(', '),
  };
}
