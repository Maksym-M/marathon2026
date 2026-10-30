using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;

namespace Restaurant_Aqa_Framework.Api;

/// <summary>Pre-order dish confirm/cancel live here, not in <see cref="OrdersApi"/> — the routes are
/// PATCH /reservations/{reservationId}/order/confirmations|cancellations/{dishId}, path-grouped under
/// Reservations in RouteRegistry regardless of the fact that they mutate an order's line items.</summary>
public sealed class ReservationsApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> CreateReservationAsync(CreateReservationRequest request, string? bearerToken = null) =>
        Api.PostAsync(Url("/reservations"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> GetReservationsAsync(string? bearerToken = null) =>
        GetAsync(Url("/reservations"), new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> GetReservationAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> EditReservationAsync(EditReservationRequest request, string? bearerToken = null) =>
        PutAsync(Url("/reservations"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> CancelReservationAsync(string reservationId, string? bearerToken = null) =>
        DeleteAsync(Url("/reservations"), new APIRequestContextOptions
        {
            DataString = Serialize(new CancelReservationRequest(reservationId)),
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> UpdateReservationStatusAsync(
        string reservationId, ReservationStatus newStatus, string? bearerToken = null) =>
        Api.PatchAsync(Url($"/reservations/{reservationId}/status"), new APIRequestContextOptions
        {
            DataString = Serialize(new UpdateReservationStatusRequest(newStatus)),
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> UpdateReservationTableAsync(
        string reservationId, string tableId, string? bearerToken = null) =>
        Api.PatchAsync(Url($"/reservations/{reservationId}/table"), new APIRequestContextOptions
        {
            DataString = Serialize(new UpdateReservationTableRequest(tableId)),
            Headers = Headers(bearerToken),
        });

    /// <summary>The token of the link the Owner shares with guests (only for reservations of 2 or more guests).</summary>
    public Task<IAPIResponse> GetInviteTokenAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/invite-token"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    /// <summary>The Owner's own session in the reservation's group; the backend creates it on the first call
    /// (the group pre-order page makes this call when the Owner opens it).</summary>
    public Task<IAPIResponse> GetOwnerSessionAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/guest-session-id"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> GetReservationFeedbackAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/feedback"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> ConfirmPreOrderDishAsync(
        string reservationId, string dishId, string? bearerToken = null) =>
        Api.PatchAsync(Url($"/reservations/{reservationId}/order/confirmations/{dishId}"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> CancelPreOrderDishAsync(
        string reservationId, string dishId, string? bearerToken = null) =>
        Api.PatchAsync(Url($"/reservations/{reservationId}/order/cancellations/{dishId}"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });
}
