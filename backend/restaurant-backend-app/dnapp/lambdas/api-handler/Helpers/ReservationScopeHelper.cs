using APIHandler.Constants;

namespace APIHandler.Helpers;

/// <summary>
/// The two tabs of the Customer's "Reservations" page (US20 AC2–AC3): "Upcoming" — Reserved or In Progress;
/// "Past" — Completed or Cancelled. <see cref="All"/> keeps the list as it was before scopes existed.
/// </summary>
public enum ReservationScope
{
    All,
    Upcoming,
    Past,
}

public static class ReservationScopeHelper
{
    public const string QueryParameter = "scope";

    /// <summary>A missing or empty value means <see cref="ReservationScope.All"/>; anything else unknown is invalid.</summary>
    public static bool TryParse(string? value, out ReservationScope scope)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "":
                scope = ReservationScope.All;
                return true;
            case "upcoming":
                scope = ReservationScope.Upcoming;
                return true;
            case "past":
                scope = ReservationScope.Past;
                return true;
            default:
                scope = ReservationScope.All;
                return false;
        }
    }

    public static bool Includes(this ReservationScope scope, ReservationStatus status)
    {
        bool isUpcoming = status is ReservationStatus.Reserved or ReservationStatus.InProgress;
        return scope switch
        {
            ReservationScope.Upcoming => isUpcoming,
            ReservationScope.Past => !isUpcoming,
            _ => true,
        };
    }
}
