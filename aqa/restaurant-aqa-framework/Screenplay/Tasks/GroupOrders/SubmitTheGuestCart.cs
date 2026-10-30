using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>A guest submits their cart to the owner. The outcome is the scenario's last response.</summary>
public sealed class SubmitTheGuestCart(string guest, bool mustSucceed = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var session = actor.Recall<GuestSession>(guest);
        var call = await new GuestSessionsApi(actor.UsingTheApi()).SubmitAsync(session.Id, session.Token);

        if (mustSucceed)
            await actor.TheLastResponse().CaptureRequiredAsync(call, $"{guest} submitting their cart");
        else
            await actor.TheLastResponse().CaptureAsync(call);
    }
}
