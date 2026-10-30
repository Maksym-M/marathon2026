namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>GET /reservations/{reservationId}/guest-session-id — the Owner's own session, created on first call.</summary>
public sealed record OwnerSessionResponse(string GuestSessionId);
