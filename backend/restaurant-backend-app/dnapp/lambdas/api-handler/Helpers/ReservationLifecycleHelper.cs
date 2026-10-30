using APIHandler.Constants;
using APIHandler.Entities;
using System;

namespace APIHandler.Helpers;

/// <summary>
/// A reservation that never started becomes "Completed" once its end time has passed (US20; General overview:
/// "visit ends → Completed"). A visit In Progress stays with its waiter, whose "Complete" also creates the report.
/// </summary>
public static class ReservationLifecycleHelper
{
    public static bool ShouldAutoComplete(Reservation reservation, DateTime utcNow) =>
        reservation.Status == ReservationStatus.Reserved && reservation.EndTime <= utcNow;

    /// <summary>The status the reservation has once auto-completion is applied: lists filter by it before loading details.</summary>
    public static ReservationStatus EffectiveStatus(Reservation reservation, DateTime utcNow) =>
        ShouldAutoComplete(reservation, utcNow) ? ReservationStatus.Completed : reservation.Status;
}
