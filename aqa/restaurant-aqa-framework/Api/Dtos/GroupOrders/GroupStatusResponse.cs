namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>GET /guest/groups/{groupReservationId}/status. <see cref="Status"/> is "Open", "Submitted" or "Closed"
/// (this one travels as a string, unlike the reservation and order statuses).</summary>
public sealed record GroupStatusResponse(int CurrentParticipants, int MaxCapacity, string Status);
