using System.Text.Json.Serialization;

namespace APIHandler.Models.GuestSessions;

/// <summary>PUT /guest/groups/{groupReservationId}/sessions/{guestSessionId}/dishes/{dishId}. 0 removes the dish.</summary>
public class UpdateSessionDishQuantityRequest
{
    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }
}
