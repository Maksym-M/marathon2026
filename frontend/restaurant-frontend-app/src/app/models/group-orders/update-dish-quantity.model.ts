/** PUT /guest/groups/{groupReservationId}/sessions/{guestSessionId}/dishes/{dishId} — request body. 0 removes the dish. */
export interface UpdateDishQuantityRequest {
  quantity: number;
}
