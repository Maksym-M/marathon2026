import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { catchError, Observable, retry, shareReplay, tap, throwError, timer } from 'rxjs';
import { TimeRange } from '@app/models/tables/time-range.model';
import {
  Reservation,
  ReservationCustomer,
  MakeReservationRequestBody,
  CancelReservationResponse
} from '@app/models/reservations/reservation.model';
import { FeedbackReservationResponseItem } from '@app/models/feedbacks/feedback-reservation-response-item.model';
import { ReservationsRequestOptions } from '@app/models/reservations/reservations-request-options.model';
import { isTransientHttpError } from '@app/shared/helpers/http-error-helpers';

const FEEDBACK_RETRY_COUNT = 2;
const FEEDBACK_RETRY_DELAY_MS = 500;
/** GET /reservations takes seconds, so the header Cart reuses a recent answer instead of asking again. */
export const RESERVATIONS_CACHE_MS = 60_000;

@Injectable({
  providedIn: 'root',
})
export class ReservationService {
  private readonly baseUrl = inject(API_BASE_URL);
  private http: HttpClient = inject(HttpClient);
  private readonly authenticationService = inject(AuthenticationService);

  /**
   * One entry per scope ('all' without one), kept per signed-in token, so another user on this browser never
   * sees these reservations.
   */
  private readonly reservationsCache = new Map<
    string,
    { token: string | undefined; requestedAt: number; reservations$: Observable<Reservation[]> }
  >();

  // makeReservation$(tableId: string, timeRange: TimeRange, guests: number): Observable<Reservation> {
  //   const url = `${this.baseUrl}/reservations`;
  //   const body = {
  //     tableId: tableId,
  //     startTime: timeRange.start,
  //     endTime: timeRange.end,
  //     guestCount: guests,
  //   };

  //   return this.http.post<Reservation>(url, body);
  // }

  makeReservation$(
    tableId: string,
    timeRange: TimeRange,
    guests: number,
    customerEmail?: string | null,
    skipErrorToast = false
  ): Observable<Reservation> {
    const url = `${this.baseUrl}/reservations`;
    const body: MakeReservationRequestBody = {
      tableId: tableId,
      startTime: timeRange.start,
      endTime: timeRange.end,
      guestCount: guests,
    };

    if (customerEmail != null) {
      body.customerEmail = customerEmail;
    }

    return this.http
      .post<Reservation>(url, body, {
        context: new HttpContext().set(SKIP_ERROR_TOAST, skipErrorToast),
      })
      .pipe(tap(() => this.clearReservationsCache()));
  }

  editReservation$(
    reservationId: string,
    timeRange: TimeRange,
    guestCount: number
  ): Observable<Reservation> {
    const url = `${this.baseUrl}/reservations`;
    const body = {
      reservationId,
      guestCount,
      startTime: timeRange.start,
      endTime: timeRange.end,
    };

    return this.http.put<Reservation>(url, body).pipe(tap(() => this.clearReservationsCache()));
  }

  cancelReservation$(reservationId: string): Observable<CancelReservationResponse> {
    const url = `${this.baseUrl}/reservations`;

    return this.http
      .delete<CancelReservationResponse>(url, { body: { id: reservationId } })
      .pipe(tap(() => this.clearReservationsCache()));
  }

  /**
   * One request at a time is shared, and its answer is reused for `RESERVATIONS_CACHE_MS`.
   * `fresh` always asks the API (the Reservations page) and refreshes what others reuse.
   */
  getReservationsForUser$({ fresh = false, scope }: ReservationsRequestOptions = {}): Observable<Reservation[]> {
    const key = scope ?? 'all';
    const cache = this.reservationsCache.get(key);
    const token = this.authenticationService.getToken();
    if (!fresh && cache && cache.token === token && Date.now() - cache.requestedAt < RESERVATIONS_CACHE_MS) {
      return cache.reservations$;
    }

    // A backend without scopes ignores the parameter and returns every reservation; callers handle both.
    const params = scope ? new HttpParams().set('scope', scope) : undefined;
    const reservations$: Observable<Reservation[]> = this.http
      .get<Reservation[]>(`${this.baseUrl}/reservations`, { params })
      .pipe(
        catchError((error: unknown) => {
          // A failed answer is never reused.
          if (this.reservationsCache.get(key)?.reservations$ === reservations$) this.reservationsCache.delete(key);
          return throwError(() => error);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    this.reservationsCache.set(key, { token, requestedAt: Date.now(), reservations$ });
    return reservations$;
  }

  clearReservationsCache(): void {
    this.reservationsCache.clear();
  }

  /**
   * Feedback is optional, so a failure never shows a toast. Retried a couple of times on errors that tend to
   * pass: the API is throttled when many requests arrive at once.
   */
  getReservationFeedback(
    reservationId: string
  ): Observable<FeedbackReservationResponseItem | null> {
    return this.http
      .get<FeedbackReservationResponseItem>(`${this.baseUrl}/reservations/${reservationId}/feedback`, {
        context: new HttpContext().set(SKIP_ERROR_TOAST, true),
      })
      .pipe(
        retry({
          count: FEEDBACK_RETRY_COUNT,
          delay: (error: unknown, attempt: number) =>
            isTransientHttpError(error) ? timer(attempt * FEEDBACK_RETRY_DELAY_MS) : throwError(() => error),
        }),
      );
  }

  getReservationById(reservationId: string): Observable<ReservationCustomer> {
    return this.http.get<ReservationCustomer>(`${this.baseUrl}/reservations/${reservationId}`);
  }

  changeReservationStatus$(id: string, newStatus: number): Observable<ReservationCustomer> {
    const url = `${this.baseUrl}/reservations/${id}/status`;
    const body = { newStatus };
    return this.http.patch<ReservationCustomer>(url, body).pipe(tap(() => this.clearReservationsCache()));
  }
}
