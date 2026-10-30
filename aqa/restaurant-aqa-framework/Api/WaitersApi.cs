using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Api;

public sealed class WaitersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    /// <summary>The location a waiter works at; <paramref name="userId"/> is the Cognito <c>sub</c> of the waiter.</summary>
    public Task<IAPIResponse> GetLocationAsync(string userId, string bearerToken) =>
        GetAsync(Url($"/waiters/{userId}/location"), new APIRequestContextOptions { Headers = Headers(bearerToken) });
}
