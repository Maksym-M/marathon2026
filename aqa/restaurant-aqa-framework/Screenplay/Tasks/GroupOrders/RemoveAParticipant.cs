using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>Someone removes a participant from the group — the owner, or anyone else trying to. The factories aim it
/// at what must be refused: a guest who never joined, or the owner's own place. A guest of another group is just
/// named (they are remembered like any guest). On success the answer is the updated participants list.</summary>
public sealed class RemoveAParticipant : ITask
{
    private readonly Requester _requester;
    private readonly Func<Actor, string> _participant;
    private readonly bool _mustSucceed;

    /// <param name="mustSucceed">True when the removal is a precondition ("the owner has removed …").</param>
    public RemoveAParticipant(string guest, Requester requester = Requester.Owner, bool mustSucceed = false)
        : this(requester, actor => actor.Recall<GuestSession>(guest).Id, mustSucceed)
    {
    }

    private RemoveAParticipant(Requester requester, Func<Actor, string> participant, bool mustSucceed = false)
    {
        _requester = requester;
        _participant = participant;
        _mustSucceed = mustSucceed;
    }

    public static RemoveAParticipant WhoNeverJoined() => new(Requester.Owner, _ => Guid.NewGuid().ToString());

    /// <summary>The owner's session id is derived from the group ("owner-{groupReservationId}").</summary>
    public static RemoveAParticipant TheOwnersOwnPlace() =>
        new(Requester.Owner, actor => $"owner-{actor.Recall<GroupBooking>(GroupBooking.TheGroup).GroupReservationId}");

    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupBooking>(GroupBooking.TheGroup).GroupReservationId;
        var participant = _participant(actor);
        var token = await actor.TokenOfAsync(_requester);

        var call = await new GroupOrdersApi(actor.UsingTheApi()).RemoveParticipantAsync(group, participant, token);

        if (_mustSucceed)
            await actor.TheLastResponse().CaptureRequiredAsync(call, "Removing a participant");
        else
            await actor.TheLastResponse().CaptureAsync(call);
    }
}
