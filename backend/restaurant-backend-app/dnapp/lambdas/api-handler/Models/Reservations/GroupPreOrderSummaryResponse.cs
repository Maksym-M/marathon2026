using APIHandler.Entities;
using System.Text.Json.Serialization;

namespace APIHandler.Models.Reservations;

/// <summary>
/// The group pre-order of a reservation as the "Reservations" list shows it (US20 AC5–AC7):
/// "Pre-orders: {Submitted}/{Seats}", and whether the invite link still has free seats.
/// </summary>
public class GroupPreOrderSummaryResponse
{
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter<GroupReservationStatus>))]
    public GroupReservationStatus Status { get; set; }

    /// <summary>Participants with a submitted pre-order; the Owner counts once they have dishes (US23 AC3).</summary>
    [JsonPropertyName("submitted")]
    public int Submitted { get; set; }

    /// <summary>The Owner plus every guest who joined.</summary>
    [JsonPropertyName("joined")]
    public int Joined { get; set; }

    /// <summary>The guests count, or the number of participants if more have joined.</summary>
    [JsonPropertyName("seats")]
    public int Seats { get; set; }
}
