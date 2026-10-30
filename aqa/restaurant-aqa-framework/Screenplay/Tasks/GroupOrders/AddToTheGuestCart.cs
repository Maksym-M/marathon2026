using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>A guest adds some of a served dish to their own cart. The outcome is the scenario's last response.</summary>
public sealed class AddToTheGuestCart(string guest, string dish, int quantity, bool mustSucceed = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var session = actor.Recall<GuestSession>(guest);
        var call = await new GuestSessionsApi(actor.UsingTheApi())
            .AddDishAsync(session.Id, actor.Recall<ServedDish>(dish).Id, quantity, session.Token);

        if (mustSucceed)
            await actor.TheLastResponse().CaptureRequiredAsync(call, $"{guest} adding {quantity} {dish}");
        else
            await actor.TheLastResponse().CaptureAsync(call);
    }
}
