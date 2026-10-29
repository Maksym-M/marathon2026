import { buildPreOrderMenuParams, PreOrderMenuReservation } from './pre-order-menu-params';

const RESERVATION: PreOrderMenuReservation = {
  id: 'res-1',
  tableId: 'table-1',
  startTime: new Date(2026, 9, 17, 12, 0),
  endTime: new Date(2026, 9, 17, 14, 0),
  guestCount: 2,
  locationAddress: '48 Rustaveli Avenue',
};

describe('buildPreOrderMenuParams', () => {
  it('enables pre-order mode for the reservation', () => {
    expect(buildPreOrderMenuParams(RESERVATION)).toEqual(
      jasmine.objectContaining({ preOrder: 'true', reservationId: 'res-1' }),
    );
  });

  it('turns on group mode for 2+ guests', () => {
    expect(buildPreOrderMenuParams(RESERVATION)['group']).toBe('true');
  });

  it('leaves group mode off for a single guest', () => {
    expect(buildPreOrderMenuParams({ ...RESERVATION, guestCount: 1 })['group']).toBeNull();
  });

  it('builds the label from address, table and time', () => {
    const label: string = buildPreOrderMenuParams(RESERVATION)['reservationLabel'];

    expect(label).toBe('48 Rustaveli Avenue, Table table-1, Sat Oct 17 2026, 12:00 - 14:00');
  });

  it('shows times in 24-hour format', () => {
    const label: string = buildPreOrderMenuParams({
      ...RESERVATION,
      startTime: new Date(2026, 9, 17, 19, 30),
      endTime: new Date(2026, 9, 17, 21, 0),
    })['reservationLabel'];

    expect(label).toContain('19:30 - 21:00');
    expect(label).not.toMatch(/AM|PM/);
  });

  it('prefers the table name, then the table number, over the table id', () => {
    const withNumber = buildPreOrderMenuParams({ ...RESERVATION, tableNumber: 5 });
    const withName = buildPreOrderMenuParams({ ...RESERVATION, tableNumber: 5, tableName: 'Window' });

    expect(withNumber['reservationLabel']).toContain('Table 5,');
    expect(withName['reservationLabel']).toContain('Window,');
  });

  it('accepts ISO strings from the API', () => {
    const params = buildPreOrderMenuParams({
      ...RESERVATION,
      startTime: '2026-10-17T12:00:00',
      endTime: '2026-10-17T14:00:00',
    });

    expect(params['reservationLabel']).toContain('Sat Oct 17 2026');
  });
});
