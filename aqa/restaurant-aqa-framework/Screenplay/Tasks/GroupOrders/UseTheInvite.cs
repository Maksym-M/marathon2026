using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>A guest goes back to the group with the session they joined with (opens its menu). The outcome is the
/// scenario's last response.</summary>
public sealed class UseTheInvite(string guest) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var session = actor.Recall<GuestSession>(guest);
        await actor.TheLastResponse().CaptureAsync(await new GuestSessionsApi(actor.UsingTheApi()).GetMenuAsync(session.Id, session.Token));
    }
}
