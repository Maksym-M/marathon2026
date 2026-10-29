import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { Observable, switchMap, throwError, timer } from 'rxjs';
import {
  AVOCADO,
  DEMO_GROUP_ID,
  DEMO_INVITE,
  DEMO_INVITE_TOKEN,
  DEMO_OWNER_SESSION_ID,
  DEMO_RESERVATION_ID,
  GroupOrdersMockStore,
  MockHttpError,
  POTATO,
  SPRING,
  STRAWBERRY,
} from './group-orders.mock-store';
import { DEMO_USER, isDemoAuthorization } from '../auth/demo-auth';

const LATENCY_MS = 400;

function demoReservations() {
  const twoWeeksAgo = new Date(Date.now() - 14 * 24 * 60 * 60 * 1000);
  const fiveDaysAgo = new Date(Date.now() - 5 * 24 * 60 * 60 * 1000);
  const base = {
    tableId: 'demo-table',
    customerId: DEMO_USER.id,
    waiterId: 'demo-waiter',
    restaurantName: DEMO_INVITE.RestaurantName,
    locationAddress: DEMO_INVITE.LocationAddress,
    tableNumber: DEMO_INVITE.TableNumber,
  };
  return [
    {
      ...base,
      id: DEMO_RESERVATION_ID,
      startTime: DEMO_INVITE.StartTime,
      endTime: DEMO_INVITE.EndTime,
      guestCount: groupOrdersMockStore.maxCapacity,
      status: 0, // Reserved
      groupPreOrder: groupOrdersMockStore.summary(),
    },
    {
      ...base,
      id: 'demo-past',
      startTime: twoWeeksAgo.toISOString(),
      endTime: new Date(twoWeeksAgo.getTime() + 90 * 60 * 1000).toISOString(),
      guestCount: 2,
      status: 3, // Completed
      groupPreOrder: null,
    },
    {
      ...base,
      id: 'demo-cancelled',
      startTime: fiveDaysAgo.toISOString(),
      endTime: new Date(fiveDaysAgo.getTime() + 90 * 60 * 1000).toISOString(),
      guestCount: 4,
      status: 1, // Cancelled
      // As the real API sends it: the group stays, closed, with nobody but the Owner.
      groupPreOrder: { status: 'Closed', submitted: 0, joined: 1, seats: 4 },
    },
  ];
}
const DEMO_MENU = [SPRING, AVOCADO, STRAWBERRY, POTATO].map((dish) => ({
  ...dish,
  state: 'Available',
  dishType: 'Appetizers',
  description: '',
  calories: '0',
  proteins: '0',
  carbohydrates: '0',
  fats: '0',
  vitamins: '0',
}));
export const groupOrdersMockStore = new GroupOrdersMockStore();

/**
 * DEV-ONLY. Перехоплює запити ЛИШЕ для демо-бронювання `demo-reservation` / `demo-group`.
 * Усі інші запити йдуть на справжній API без змін.
 * Відкрий: /reservations/demo-reservation/group-order?demo  (?demo — мок-логін, див. shared/mocks/auth/demo-auth.ts)
 */
export const groupOrdersMockInterceptor: HttpInterceptorFn = (req, next) => {
  const path = req.url.split('?')[0];
  const store = groupOrdersMockStore;
  const groupBase = `/guest/groups/${DEMO_GROUP_ID}/sessions`;

  const respond = (handler: () => unknown): Observable<HttpResponse<unknown>> =>
    timer(LATENCY_MS).pipe(
      switchMap(() => {
        try {
          return [new HttpResponse({ status: 200, url: req.url, body: handler() })];
        } catch (error) {
          if (error instanceof MockHttpError) {
            return throwError(() => new HttpErrorResponse({ status: error.status, url: req.url, error: { error: error.message } }));
          }
          throw error;
        }
      }),
    );

  if (req.method === 'GET' && path.endsWith(`/reservations/${DEMO_RESERVATION_ID}`)) {
    return respond(() => ({
      id: DEMO_RESERVATION_ID,
      tableId: 'demo-table',
      startTime: DEMO_INVITE.StartTime,
      endTime: DEMO_INVITE.EndTime,
      guestCount: store.maxCapacity,
      status: 0, // Reserved
      restaurantName: DEMO_INVITE.RestaurantName,
      locationAddress: DEMO_INVITE.LocationAddress,
      waiterId: 'demo-waiter',
    }));
  }
  if (req.method === 'GET' && path.endsWith(`/reservations/${DEMO_RESERVATION_ID}/invite-token`)) {
    return respond(() => ({ token: DEMO_INVITE_TOKEN }));
  }
  if (req.method === 'GET' && path.endsWith(`/guest/invites/${DEMO_INVITE_TOKEN}`)) {
    return respond(() => ({ ...DEMO_INVITE, MaxGuestCount: store.maxCapacity }));
  }
  if (req.method === 'GET' && path.endsWith(`/reservations/${DEMO_RESERVATION_ID}/guest-session-id`)) {
    return respond(() => ({ guestSessionId: DEMO_OWNER_SESSION_ID }));
  }
  if (req.method === 'GET' && path.endsWith(groupBase)) {
    return respond(() => store.snapshot());
  }
  if (req.method === 'GET' && path.endsWith(`/guest/groups/${DEMO_GROUP_ID}/status`)) {
    return respond(() => store.groupStatus());
  }
  if (req.method === 'POST' && path.endsWith(`/group-reservations/${DEMO_GROUP_ID}/submit`)) {
    return respond(() => store.submit((req.body as { reservationId?: string } | null)?.reservationId ?? ''));
  }
  const dishPath = path.match(new RegExp(`${groupBase}/([^/]+)/dishes/([^/]+)$`));
  if (req.method === 'PUT' && dishPath) {
    const quantity = (req.body as { quantity?: number } | null)?.quantity ?? -1;
    return respond(() => store.updateDish(decodeURIComponent(dishPath[1]), decodeURIComponent(dishPath[2]), quantity));
  }
  if (req.method === 'DELETE' && path.endsWith(`/guest/groups/${DEMO_GROUP_ID}/seats`)) {
    return respond(() => store.removeSeat());
  }
  if (req.method === 'DELETE' && path.includes(`${groupBase}/`)) {
    const guestSessionId = decodeURIComponent(path.slice(path.lastIndexOf('/') + 1));
    return respond(() => store.remove(guestSessionId));
  }

  // Демо-логін (?demo): справжній API демо-токен не прийме, тож кошик у шапці показуємо порожнім.
  if (req.method === 'GET' && path.endsWith('/orders') && isDemoAuthorization(req.headers.get('Authorization'))) {
    return respond(() => []);
  }
  // …сторінка Reservations (US20): демо-бронювання з груповим pre-order і одне минуле.
  if (req.method === 'GET' && path.endsWith('/reservations') && isDemoAuthorization(req.headers.get('Authorization'))) {
    return respond(() => demoReservations());
  }
  // Демо-бронювання ще без відгуку.
  if (req.method === 'GET' && /\/reservations\/demo-[^/]+\/feedback$/.test(path)) {
    return respond(() => null);
  }
  // …і меню під кошиком (/reservations/demo-reservation/group-order) — з демо-страв.
  if (req.method === 'GET' && path.endsWith('/dishes') && isDemoAuthorization(req.headers.get('Authorization'))) {
    return respond(() => DEMO_MENU);
  }

  return next(req);
};
