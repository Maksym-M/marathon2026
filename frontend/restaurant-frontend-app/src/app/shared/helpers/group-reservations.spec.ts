import { Reservation, ReservationStatus } from '@app/models/reservations/reservation.model';
import { findNearestGroupReservationId } from './group-reservations';

const NOW = new Date('2030-10-17T12:00:00Z');

const reservation = (id: string, start: string, guestCount: number, status = ReservationStatus.Reserved): Reservation =>
  ({
    id,
    startTime: new Date(start),
    endTime: new Date(new Date(start).getTime() + 90 * 60 * 1000),
    guestCount,
    status,
  }) as Reservation;

describe('findNearestGroupReservationId', () => {
  const cases: { name: string; reservations: Reservation[]; expected: string | null }[] = [
    { name: 'no reservations', reservations: [], expected: null },
    { name: 'only 1-guest reservations', reservations: [reservation('a', '2030-10-18T12:00:00Z', 1)], expected: null },
    {
      name: 'nearest upcoming group reservation wins',
      reservations: [reservation('later', '2030-10-20T12:00:00Z', 4), reservation('sooner', '2030-10-18T12:00:00Z', 2)],
      expected: 'sooner',
    },
    {
      name: 'cancelled and finished ones are skipped',
      reservations: [
        reservation('cancelled', '2030-10-18T12:00:00Z', 4, ReservationStatus.Cancelled),
        reservation('finished', '2030-10-18T13:00:00Z', 4, ReservationStatus.Finished),
        reservation('ok', '2030-10-19T12:00:00Z', 3),
      ],
      expected: 'ok',
    },
    { name: 'already ended is skipped', reservations: [reservation('past', '2030-10-16T12:00:00Z', 4)], expected: null },
    { name: 'running now (not ended yet) still counts', reservations: [reservation('now', '2030-10-17T11:30:00Z', 4)], expected: 'now' },
  ];

  for (const { name, reservations, expected } of cases) {
    it(name, () => expect(findNearestGroupReservationId(reservations, NOW)).toBe(expected));
  }
});
