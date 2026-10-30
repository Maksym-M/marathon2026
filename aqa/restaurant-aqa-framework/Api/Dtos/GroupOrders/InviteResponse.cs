namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>GET /guest/invites/{token}. The handler answers in PascalCase; <see cref="JsonDefaults"/> reads it
/// case-insensitively. Only the fields the suite uses are modelled.</summary>
public sealed record InviteResponse(string GroupReservationId, int MaxGuestCount, int TableNumber);
