namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>POST /guest/sessions — a guest joins the group under a display name.</summary>
public sealed record JoinGroupRequest(string GroupReservationId, string GuestName);
