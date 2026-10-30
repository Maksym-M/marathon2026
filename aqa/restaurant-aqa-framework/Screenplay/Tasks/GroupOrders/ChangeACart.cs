using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>The owner changes how much of a dish is in a cart — a guest's submitted cart, or the owner's own pre-order
/// (<paramref name="participant"/> = <see cref="CustomerAccount.Owner"/>); 0 takes the dish out. A guest's cart left
/// empty goes back to not submitted. The outcome is the scenario's last response.</summary>
public sealed class ChangeACart(string participant, string dish, int quantity) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var owner = actor.Recall<CustomerAccount>(CustomerAccount.Owner);
        var group = actor.Recall<GroupBooking>(GroupBooking.TheGroup).GroupReservationId;
        var session = participant == CustomerAccount.Owner ? $"owner-{group}" : actor.Recall<GuestSession>(participant).Id;

        await actor.TheLastResponse().CaptureRequiredAsync(
            await new GroupOrdersApi(actor.UsingTheApi()).SetDishQuantityAsync(
                group, session, actor.Recall<ServedDish>(dish).Id, quantity, owner.IdToken),
            $"The owner changing the {dish} in the {participant}'s cart");
    }
}
