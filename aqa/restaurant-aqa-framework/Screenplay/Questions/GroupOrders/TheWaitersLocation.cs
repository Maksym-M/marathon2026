using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Waiters;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>The location the standing waiter works at. Group bookings are made there, so the reservation is always
/// assigned to that waiter and "the waiter sees the pre-order" has one answer. Asked once, then remembered.</summary>
public sealed class TheWaitersLocation : IQuestion<WaiterLocationResponse>
{
    public async Task<WaiterLocationResponse> AnsweredByAsync(Actor actor)
    {
        if (actor.TryRecall<WaiterLocationResponse>(out var location))
            return location;

        var waiter = await actor.AsksForAsync(new TheWaiterSession());
        var response = await new WaitersApi(actor.UsingTheApi()).GetLocationAsync(JwtClaims.Subject(waiter.IdToken), waiter.IdToken);
        if (!response.Ok)
            throw new InvalidOperationException($"Looking up the waiter's location failed with {response.Status}: {await response.ReadErrorMessageAsync()}");

        location = await response.ReadAsAsync<WaiterLocationResponse>();
        actor.Remember(location);
        return location;
    }
}
