import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { splitReservationsByTab } from './reservation-tabs';

const reservation = (id: string, start: string, status: ReservationStatusCustomer) =>
  ({ id, startTime: new Date(start), status }) as ReservationCustomer;

const ids = (list: ReservationCustomer[]) => list.map((item) => item.id);

describe('splitReservationsByTab', () => {
  it('empty list → both tabs empty', () => {
    expect(splitReservationsByTab([])).toEqual({ upcoming: [], past: [] });
  });

  it('Reserved and In Progress go to Upcoming; Completed and Cancelled to Past', () => {
    const { upcoming, past } = splitReservationsByTab([
      reservation('reserved', '2030-10-18T12:00:00Z', ReservationStatusCustomer.Reserved),
      reservation('in-progress', '2030-10-17T12:00:00Z', ReservationStatusCustomer.InProgress),
      reservation('completed', '2030-10-10T12:00:00Z', ReservationStatusCustomer.Finished),
      reservation('cancelled', '2030-10-12T12:00:00Z', ReservationStatusCustomer.Cancelled),
    ]);
    expect(ids(upcoming)).toEqual(['in-progress', 'reserved']);
    expect(ids(past)).toEqual(['cancelled', 'completed']);
  });

  it('Upcoming: nearest first, also by time within one day', () => {
    const { upcoming } = splitReservationsByTab([
      reservation('evening', '2030-10-17T19:00:00Z', ReservationStatusCustomer.Reserved),
      reservation('next-week', '2030-10-24T10:00:00Z', ReservationStatusCustomer.Reserved),
      reservation('morning', '2030-10-17T09:00:00Z', ReservationStatusCustomer.Reserved),
    ]);
    expect(ids(upcoming)).toEqual(['morning', 'evening', 'next-week']);
  });

  it('Past & Cancelled: most recent first', () => {
    const { past } = splitReservationsByTab([
      reservation('old', '2030-01-01T12:00:00Z', ReservationStatusCustomer.Finished),
      reservation('recent', '2030-10-01T12:00:00Z', ReservationStatusCustomer.Finished),
    ]);
    expect(ids(past)).toEqual(['recent', 'old']);
  });

  it('does not change the input order', () => {
    const list = [
      reservation('b', '2030-10-18T12:00:00Z', ReservationStatusCustomer.Reserved),
      reservation('a', '2030-10-17T12:00:00Z', ReservationStatusCustomer.Reserved),
    ];
    splitReservationsByTab(list);
    expect(ids(list)).toEqual(['b', 'a']);
  });
});
