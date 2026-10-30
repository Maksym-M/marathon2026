namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>POST /group-reservations/{groupReservationId}/submit — the reservation the group belongs to.</summary>
public sealed record SubmitGroupPreOrderRequest(string ReservationId);
