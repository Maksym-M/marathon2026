using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>The owner takes their own place in the group — what opening the group pre-order page does first.</summary>
public sealed class TakeAPlaceInTheGroup : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var owner = actor.Recall<CustomerAccount>(CustomerAccount.Owner);
        var booking = actor.Recall<GroupBooking>(GroupBooking.TheGroup);
        await actor.TheLastResponse().CaptureRequiredAsync(
            await new ReservationsApi(actor.UsingTheApi()).GetOwnerSessionAsync(booking.ReservationId, owner.IdToken),
            "The owner taking their place in the group");
    }
}
