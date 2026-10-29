import { DEMO_RESERVATION_ID, GroupOrdersMockStore, MockHttpError } from './group-orders.mock-store';

describe('GroupOrdersMockStore', () => {
  let store: GroupOrdersMockStore;
  beforeEach(() => (store = new GroupOrdersMockStore()));

  it('returns totals per participant and for the whole group', () => {
    const snapshot = store.snapshot();
    expect(snapshot.participants.map((p) => p.total)).toEqual([26.5, 24, 24, 0]);
    expect(snapshot.total).toBe(74.5);
  });

  it('submits only submitted sessions, merged by dishId', () => {
    expect(store.submit(DEMO_RESERVATION_ID)).toEqual([
      { dishId: 'dish-spring-salad', quantity: 3 },
      { dishId: 'dish-avocado-bowl', quantity: 1 },
    ]);
    expect(store.groupStatus().status).toBe('Submitted');
  });

  it('rejects a second submit with 400', () => {
    store.submit(DEMO_RESERVATION_ID);
    expect(() => store.submit(DEMO_RESERVATION_ID)).toThrow(new MockHttpError(400, 'Group reservation was submitted.'));
  });

  it('removes a guest and returns an updated snapshot', () => {
    const snapshot = store.remove('gs-mariia');
    expect(snapshot.participants.length).toBe(3);
    expect(snapshot.total).toBe(50.5);
  });

  it('returns 404 for an unknown session', () => {
    expect(() => store.remove('nope')).toThrowMatching((e) => e instanceof MockHttpError && e.status === 404);
  });

  it('owner changes a dish quantity in a submitted guest cart', () => {
    const snapshot = store.updateDish('gs-kateryna', 'dish-spring-salad', 3);
    expect(snapshot.participants.find((p) => p.id === 'gs-kateryna')?.total).toBe(36);
  });

  it('quantity 0 removes the dish; an emptied guest cart is no longer submitted', () => {
    const kateryna = store.updateDish('gs-kateryna', 'dish-spring-salad', 0).participants.find((p) => p.id === 'gs-kateryna');
    expect(kateryna?.items).toEqual([]);
    expect(kateryna?.isSubmitted).toBeFalse();
  });

  it('owner edits their own cart', () => {
    const owner = store.updateDish('gs-owner', 'dish-avocado-bowl', 2).participants[0];
    expect(owner.total).toBe(41);
  });

  it('rejects edits of a guest cart that is not submitted', () => {
    expect(() => store.updateDish('gs-mariia', 'dish-strawberry-salad', 2)).toThrowMatching(
      (e) => e instanceof MockHttpError && e.status === 400,
    );
  });

  it('rejects a dish that is not in the cart, and a negative quantity', () => {
    expect(() => store.updateDish('gs-owner', 'nope', 1)).toThrowMatching((e) => e instanceof MockHttpError && e.status === 404);
    expect(() => store.updateDish('gs-owner', 'dish-spring-salad', -1)).toThrowMatching(
      (e) => e instanceof MockHttpError && e.status === 400,
    );
  });

  it('after submit, edits and removals are rejected', () => {
    store.submit(DEMO_RESERVATION_ID);
    expect(() => store.updateDish('gs-owner', 'dish-spring-salad', 2)).toThrowMatching(
      (e) => e instanceof MockHttpError && e.status === 400,
    );
    expect(() => store.remove('gs-mariia')).toThrowMatching((e) => e instanceof MockHttpError && e.status === 400);
  });

  it('removes an empty seat: the guests count goes down by 1', () => {
    expect(store.removeSeat().maxCapacity).toBe(4);
    expect(store.groupStatus().maxCapacity).toBe(4);
  });

  it('rejects removing a seat when every seat has a participant', () => {
    store.removeSeat();
    expect(() => store.removeSeat()).toThrowMatching((e) => e instanceof MockHttpError && e.status === 400);
  });

  it('summary for the Reservations list: Owner with dishes + submitted guests, joined, seats', () => {
    expect(store.summary()).toEqual({ status: 'Open', submitted: 2, joined: 4, seats: 5 });
  });
});
