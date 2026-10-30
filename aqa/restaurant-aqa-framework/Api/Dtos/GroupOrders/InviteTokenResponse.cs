namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>GET /reservations/{reservationId}/invite-token — the token in the link the Owner shares with guests.</summary>
public sealed record InviteTokenResponse(string Token);
