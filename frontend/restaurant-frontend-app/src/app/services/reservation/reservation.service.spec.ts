import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReservationService } from './reservation.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { Reservation } from '@app/models/reservations/reservation.model';
import { RESERVATIONS_CACHE_MS } from './reservation.service';

const TIME_RANGE = { start: '2026-10-17T12:00:00.000Z', end: '2026-10-17T13:30:00.000Z' };

describe('ReservationService', () => {
  let service: ReservationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(ReservationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('makeReservation$', () => {
    it('posts the table, time range and guest count', () => {
      service.makeReservation$('table-1', TIME_RANGE, 2).subscribe();

      const req = httpMock.expectOne('https://test.api/reservations');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        tableId: 'table-1',
        startTime: TIME_RANGE.start,
        endTime: TIME_RANGE.end,
        guestCount: 2,
      });
      req.flush({});
    });

    it('keeps the global error toast by default', () => {
      service.makeReservation$('table-1', TIME_RANGE, 2).subscribe();

      const req = httpMock.expectOne('https://test.api/reservations');
      expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeFalse();
      req.flush({});
    });

    it('marks the request to skip the global error toast when asked', () => {
      service.makeReservation$('table-1', TIME_RANGE, 2, null, true).subscribe();

      const req = httpMock.expectOne('https://test.api/reservations');
      expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
      req.flush({});
    });
  });
  describe('getReservationFeedback', () => {
    const FEEDBACK_URL = 'https://test.api/reservations/res-1/feedback';

    it('never shows the global error toast: feedback is optional', () => {
      service.getReservationFeedback('res-1').subscribe();

      const req = httpMock.expectOne(FEEDBACK_URL);
      expect(req.request.method).toBe('GET');
      expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
      req.flush({});
    });

    it('retries a request that got no response (0) or a server error, then succeeds', fakeAsync(() => {
      let result: unknown;
      service.getReservationFeedback('res-1').subscribe((feedback) => (result = feedback));

      httpMock.expectOne(FEEDBACK_URL).flush(null, { status: 0, statusText: 'Unknown Error' });
      tick(500);
      httpMock.expectOne(FEEDBACK_URL).flush(null, { status: 503, statusText: 'Service Unavailable' });
      tick(1000);
      httpMock.expectOne(FEEDBACK_URL).flush({ service: null });

      expect(result).toEqual({ service: null });
    }));

    it('gives up after 2 retries', fakeAsync(() => {
      let status: number | undefined;
      service.getReservationFeedback('res-1').subscribe({ error: (error) => (status = error.status) });

      httpMock.expectOne(FEEDBACK_URL).flush(null, { status: 500, statusText: 'Server Error' });
      tick(500);
      httpMock.expectOne(FEEDBACK_URL).flush(null, { status: 500, statusText: 'Server Error' });
      tick(1000);
      httpMock.expectOne(FEEDBACK_URL).flush(null, { status: 500, statusText: 'Server Error' });

      expect(status).toBe(500);
    }));

    it('does not retry a 404: there is simply no feedback yet', () => {
      let status: number | undefined;
      service.getReservationFeedback('res-1').subscribe({ error: (error) => (status = error.status) });

      httpMock.expectOne(FEEDBACK_URL).flush(null, { status: 404, statusText: 'Not Found' });

      expect(status).toBe(404);
    });
  });

  describe('getReservationsForUser$ cache', () => {
    const LIST_URL = 'https://test.api/reservations';
    const LIST = [{ id: 'res-1' }] as Reservation[];

    it('reuses a recent answer: the second caller gets it without a request', () => {
      service.getReservationsForUser$().subscribe();
      httpMock.expectOne(LIST_URL).flush(LIST);

      let reused: Reservation[] | undefined;
      service.getReservationsForUser$().subscribe((reservations) => (reused = reservations));

      httpMock.expectNone(LIST_URL);
      expect(reused).toEqual(LIST);
    });

    it('callers during a request share it', () => {
      service.getReservationsForUser$().subscribe();
      service.getReservationsForUser$().subscribe();

      httpMock.expectOne(LIST_URL).flush(LIST);
    });

    it('asks again once the answer is older than the cache time', () => {
      const now = Date.now();
      const clock = spyOn(Date, 'now').and.returnValue(now);
      service.getReservationsForUser$().subscribe();
      httpMock.expectOne(LIST_URL).flush(LIST);

      clock.and.returnValue(now + RESERVATIONS_CACHE_MS + 1);
      service.getReservationsForUser$().subscribe();

      httpMock.expectOne(LIST_URL).flush(LIST);
    });

    it('fresh always asks the API', () => {
      service.getReservationsForUser$().subscribe();
      httpMock.expectOne(LIST_URL).flush(LIST);

      service.getReservationsForUser$({ fresh: true }).subscribe();

      httpMock.expectOne(LIST_URL).flush(LIST);
    });

    it('never reuses another signed-in user\'s answer', () => {
      const auth = TestBed.inject(AuthenticationService);
      const token = spyOn(auth, 'getToken').and.returnValue('user-a');
      service.getReservationsForUser$().subscribe();
      httpMock.expectOne(LIST_URL).flush(LIST);

      token.and.returnValue('user-b');
      service.getReservationsForUser$().subscribe();

      httpMock.expectOne(LIST_URL).flush([]);
    });

    it('never reuses a failed answer', () => {
      service.getReservationsForUser$().subscribe({ error: () => undefined });
      httpMock.expectOne(LIST_URL).flush(null, { status: 500, statusText: 'Server Error' });

      service.getReservationsForUser$().subscribe();

      httpMock.expectOne(LIST_URL).flush(LIST);
    });

    it('a scope goes to the API as ?scope= and has its own cache entry', () => {
      service.getReservationsForUser$({ scope: 'upcoming' }).subscribe();
      const upcoming = httpMock.expectOne((req) => req.url === LIST_URL && req.params.get('scope') === 'upcoming');
      upcoming.flush(LIST);

      service.getReservationsForUser$({ scope: 'upcoming' }).subscribe();
      service.getReservationsForUser$({ scope: 'past' }).subscribe();

      httpMock.expectOne((req) => req.url === LIST_URL && req.params.get('scope') === 'past').flush([]);
    });

    it('without a scope there is no ?scope= at all', () => {
      service.getReservationsForUser$().subscribe();

      const req = httpMock.expectOne(LIST_URL);
      expect(req.request.params.has('scope')).toBeFalse();
      req.flush(LIST);
    });

    it('booking, editing and cancelling make the next caller ask again', () => {
      service.getReservationsForUser$().subscribe();
      httpMock.expectOne(LIST_URL).flush(LIST);

      service.cancelReservation$('res-1').subscribe();
      httpMock.expectOne((req) => req.method === 'DELETE').flush({ message: 'Cancelled' });
      service.getReservationsForUser$().subscribe();

      httpMock.expectOne((req) => req.method === 'GET' && req.url === LIST_URL).flush(LIST);
    });
  });
});
