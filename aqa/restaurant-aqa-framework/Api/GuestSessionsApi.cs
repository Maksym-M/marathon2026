using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Api;

/// <summary>A guest's side of a group pre-order: following the invite link, joining, building a cart and
/// submitting it. Joining needs no account; every later call bears the session token the join handed out
/// (not a Cognito token). The frontend uses /guest/sessions/{id}/dishes, not the /guest-sessions/… spelling in
/// ApiRoutes.md, so this client does too.</summary>
public sealed class GuestSessionsApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> GetInviteAsync(string inviteToken) =>
        GetAsync(Url($"/guest/invites/{inviteToken}"), new APIRequestContextOptions { Headers = Headers() });

    public Task<IAPIResponse> JoinAsync(string groupReservationId, string guestName) =>
        Api.PostAsync(Url("/guest/sessions"), new APIRequestContextOptions
        {
            DataString = Serialize(new JoinGroupRequest(groupReservationId, guestName)),
            Headers = Headers(),
        });

    public Task<IAPIResponse> GetMenuAsync(string guestSessionId, string sessionToken) =>
        GetAsync(Url($"/guest/sessions/{guestSessionId}/menu"), new APIRequestContextOptions
        {
            Headers = Headers(sessionToken),
        });

    public Task<IAPIResponse> AddDishAsync(string guestSessionId, string dishId, int quantity, string sessionToken) =>
        Api.PostAsync(Url($"/guest/sessions/{guestSessionId}/dishes"), new APIRequestContextOptions
        {
            DataString = Serialize(new GuestDishRequest(dishId, quantity)),
            Headers = Headers(sessionToken),
        });

    public Task<IAPIResponse> SubmitAsync(string guestSessionId, string sessionToken) =>
        Api.PostAsync(Url($"/guest/sessions/{guestSessionId}/submit"), new APIRequestContextOptions
        {
            DataString = "{}",
            Headers = Headers(sessionToken),
        });
}
