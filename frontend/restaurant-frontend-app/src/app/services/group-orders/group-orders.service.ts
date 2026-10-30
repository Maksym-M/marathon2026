import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable, switchMap } from 'rxjs';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { GroupInviteDetails } from '@app/models/group-orders/group-invite-details.model';
import { GroupRequestOptions } from '@app/models/group-orders/group-request-options.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { OwnerSessionResponse } from '@app/models/group-orders/owner-session.model';
import { Order } from '@app/models/orders/order.model';
import { SubmitGroupPreOrderRequest, SubmittedDish } from '@app/models/group-orders/submit-group-pre-order.model';
import { UpdateDishQuantityRequest } from '@app/models/group-orders/update-dish-quantity.model';
import { ReservationCustomer } from '@app/models/reservations/reservation.model';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

@Injectable({
  providedIn: 'root',
})
export class GroupOrdersService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);
  private readonly guestInviteService = inject(GuestInviteService);

  /**
   * Silent: the cart and the Group Pre-Order page show their own states, and a submitted group answers
   * 400 `GroupReservationSubmitted` here, which is not an error for the Owner.
   */
  getGroupInvite$(reservationId: string): Observable<GroupInviteDetails> {
    const options = { silent: true };
    return this.guestInviteService
      .getInviteToken$(reservationId, options)
      .pipe(
        switchMap(({ token }) =>
          this.guestInviteService.getInvite$(token, options).pipe(map((invite) => ({ token, invite }))),
        ),
      );
  }

  /** The reservation behind the group (status, guests count). Silent: the Group Pre-Order page shows its own errors. */
  getReservation$(reservationId: string): Observable<ReservationCustomer> {
    return this.http.get<ReservationCustomer>(`${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}`, {
      context: this.context({ silent: true }),
    });
  }

  /** The reservation's Order: after Submit it holds every submitted dish. Silent, like `getReservation$`. */
  getOrder$(reservationId: string): Observable<Order> {
    return this.http.get<Order>(`${this.baseUrl}/orders/${encodeURIComponent(reservationId)}`, {
      context: this.context({ silent: true }),
    });
  }

  getSessions$(groupReservationId: string, options: GroupRequestOptions = {}): Observable<GroupSessionsResponse> {
    return this.http.get<GroupSessionsResponse>(this.sessionsUrl(groupReservationId), {
      context: this.context(options),
    });
  }

  getOwnerSessionId$(reservationId: string, options: GroupRequestOptions = {}): Observable<string> {
    return this.http
      .get<OwnerSessionResponse>(`${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`, {
        context: this.context(options),
      })
      .pipe(map(({ guestSessionId }) => guestSessionId));
  }

  getStatus$(groupReservationId: string, options: GroupRequestOptions = {}): Observable<GroupStatusResponse> {
    return this.http.get<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/status`,
      { context: this.context(options) },
    );
  }

  submit$(groupReservationId: string, reservationId: string): Observable<SubmittedDish[]> {
    const body: SubmitGroupPreOrderRequest = { reservationId };
    return this.http.post<SubmittedDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupReservationId)}/submit`,
      body,
    );
  }

  removeSession$(groupReservationId: string, guestSessionId: string): Observable<GroupSessionsResponse> {
    return this.http.delete<GroupSessionsResponse>(
      `${this.sessionsUrl(groupReservationId)}/${encodeURIComponent(guestSessionId)}`,
    );
  }

  /** Removes one seat nobody has joined; returns the group status with the new `maxCapacity`. */
  removeSeat$(groupReservationId: string): Observable<GroupStatusResponse> {
    return this.http.delete<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/seats`,
    );
  }

  updateDishQuantity$(
    groupReservationId: string,
    guestSessionId: string,
    dishId: string,
    quantity: number,
  ): Observable<GroupSessionsResponse> {
    const body: UpdateDishQuantityRequest = { quantity };
    return this.http.put<GroupSessionsResponse>(
      `${this.sessionsUrl(groupReservationId)}/${encodeURIComponent(guestSessionId)}/dishes/${encodeURIComponent(dishId)}`,
      body,
    );
  }

  private sessionsUrl(groupReservationId: string): string {
    return `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions`;
  }

  private context({ silent = false }: GroupRequestOptions): HttpContext {
    return new HttpContext().set(SKIP_ERROR_TOAST, silent);
  }
}
