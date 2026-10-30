using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Api;

/// <summary>The Owner's side of a group pre-order: who joined and what they ordered, editing and removing
/// participants, and sending the consolidated pre-order to the restaurant. Every route takes the Owner's
/// Cognito id token; anyone else gets the same 404 as for a group that does not exist (401 without a token).
/// The status lives under /guest/groups/… — the /group-reservations/{id}/status route in ApiRoutes.md is not
/// deployed (403 from the gateway).</summary>
public sealed class GroupOrdersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> GetParticipantsAsync(string groupReservationId, string? bearerToken = null) =>
        GetAsync(Url($"/guest/groups/{groupReservationId}/sessions"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> RemoveParticipantAsync(string groupReservationId, string guestSessionId, string? bearerToken = null) =>
        DeleteAsync(Url($"/guest/groups/{groupReservationId}/sessions/{guestSessionId}"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> SetDishQuantityAsync(
        string groupReservationId, string guestSessionId, string dishId, int quantity, string? bearerToken = null) =>
        PutAsync(Url($"/guest/groups/{groupReservationId}/sessions/{guestSessionId}/dishes/{dishId}"), new APIRequestContextOptions
        {
            DataString = Serialize(new DishQuantityRequest(quantity)),
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> GetStatusAsync(string groupReservationId, string? bearerToken = null) =>
        GetAsync(Url($"/guest/groups/{groupReservationId}/status"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> SubmitAsync(string groupReservationId, string reservationId, string? bearerToken = null) =>
        Api.PostAsync(Url($"/group-reservations/{groupReservationId}/submit"), new APIRequestContextOptions
        {
            DataString = Serialize(new SubmitGroupPreOrderRequest(reservationId)),
            Headers = Headers(bearerToken),
        });
}
