namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>PUT /guest/groups/{groupReservationId}/sessions/{guestSessionId}/dishes/{dishId} — 0 removes the dish.</summary>
public sealed record DishQuantityRequest(int Quantity);
