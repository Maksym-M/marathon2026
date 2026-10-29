// Рівень 2 · сервіс GroupOrdersService: URL, метод, тіло кожного запиту.
// Запуск: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupInviteDetails } from '@app/models/group-orders/group-invite-details.model';
import { GroupOrdersService } from './group-orders.service';

const BASE_URL = 'https://api.test';

describe('Рівень 2 · GroupOrdersService', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: BASE_URL }],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getGroupInvite$: reservationId → invite-token → invite; повертає і token (для Invite Guests)', () => {
    let details: GroupInviteDetails | undefined;
    service.getGroupInvite$('res-1').subscribe((value) => (details = value));

    http.expectOne(`${BASE_URL}/reservations/res-1/invite-token`).flush({ token: 'tok' });
    http.expectOne(`${BASE_URL}/guest/invites/tok`).flush({ GroupReservationId: 'grp-1' });

    expect(details?.token).toBe('tok');
    expect(details?.invite.GroupReservationId).toBe('grp-1');
  });

  it('getGroupInvite$: без toast — кошик і сторінка показують власні стани', () => {
    service.getGroupInvite$('res-1').subscribe();

    const tokenReq = http.expectOne(`${BASE_URL}/reservations/res-1/invite-token`);
    expect(tokenReq.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    tokenReq.flush({ token: 'tok' });
    const inviteReq = http.expectOne(`${BASE_URL}/guest/invites/tok`);
    expect(inviteReq.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    inviteReq.flush({ GroupReservationId: 'grp-1' });
  });

  it('getSessions$: GET /guest/groups/{id}/sessions', () => {
    service.getSessions$('grp-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions`);
    expect(req.request.method).toBe('GET');
    req.flush({ participants: [], total: 0 });
  });

  it('getOwnerSessionId$: повертає рядок guestSessionId', () => {
    let id = '';
    service.getOwnerSessionId$('res-1').subscribe((value) => (id = value));
    const req = http.expectOne(`${BASE_URL}/reservations/res-1/guest-session-id`);
    expect(req.request.method).toBe('GET');
    req.flush({ guestSessionId: 'owner-1' });
    expect(id).toBe('owner-1');
  });

  it('getStatus$: GET /guest/groups/{id}/status', () => {
    service.getStatus$('grp-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/status`);
    expect(req.request.method).toBe('GET');
    req.flush({ currentParticipants: 3, maxCapacity: 5, status: 'Open' });
  });

  it('submit$: POST /group-reservations/{id}/submit з тілом { reservationId }', () => {
    service.submit$('grp-1', 'res-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/group-reservations/grp-1/submit`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ reservationId: 'res-1' });
    req.flush([]);
  });

  it('getOrder$: GET /orders/{reservationId} без toast', () => {
    service.getOrder$('res-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/orders/res-1`);
    expect(req.request.method).toBe('GET');
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush({ orderId: 'order-1', orderItems: [] });
  });

  it('id у шляху кодується: «a/b» не ламає URL', () => {
    service.getSessions$('a/b').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/a%2Fb/sessions`);
    expect(req.request.url).toBe(`${BASE_URL}/guest/groups/a%2Fb/sessions`);
    req.flush({ participants: [], total: 0 });
  });
});
