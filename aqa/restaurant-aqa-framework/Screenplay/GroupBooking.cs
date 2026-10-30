namespace Restaurant_Aqa_Framework.Screenplay;

/// <summary>A reservation for several guests and the group pre-order that comes with it. Always remembered under a
/// name — <see cref="TheGroup"/> unless the scenario needs a second, independent one (<see cref="AnotherGroup"/>) —
/// so setting up the second never changes which one "the group" is.</summary>
public sealed record GroupBooking(string ReservationId, string GroupReservationId, string TableId, int TableNumber, int Seats)
{
    public const string TheGroup = "the group";
    public const string AnotherGroup = "another group";
}
