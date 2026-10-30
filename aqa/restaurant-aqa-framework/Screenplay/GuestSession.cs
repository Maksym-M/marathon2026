namespace Restaurant_Aqa_Framework.Screenplay;

/// <summary>A guest who joined a group through its invite link, remembered under their name. <see cref="Token"/>
/// is their bearer for the guest routes; the Owner refers to them by <see cref="Id"/>.</summary>
public sealed record GuestSession(string Name, string Id, string Token, string GroupReservationId);
