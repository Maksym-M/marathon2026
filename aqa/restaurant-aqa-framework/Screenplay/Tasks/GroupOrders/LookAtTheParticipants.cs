using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>Someone asks who is in the group and what they ordered — the owner, or anyone else trying to. The answer
/// is the scenario's last response (read it with <c>TheParticipants</c>).</summary>
public sealed class LookAtTheParticipants : ITask
{
    private readonly Requester _requester;
    private readonly bool _ofAGroupThatDoesNotExist;

    public LookAtTheParticipants(Requester requester = Requester.Owner)
        : this(requester, ofAGroupThatDoesNotExist: false)
    {
    }

    private LookAtTheParticipants(Requester requester, bool ofAGroupThatDoesNotExist)
    {
        _requester = requester;
        _ofAGroupThatDoesNotExist = ofAGroupThatDoesNotExist;
    }

    public static LookAtTheParticipants OfAGroupThatDoesNotExist() => new(Requester.Owner, ofAGroupThatDoesNotExist: true);

    public async Task PerformAsAsync(Actor actor)
    {
        var group = _ofAGroupThatDoesNotExist
            ? Guid.NewGuid().ToString()
            : actor.Recall<GroupBooking>(GroupBooking.TheGroup).GroupReservationId;
        var token = await actor.TokenOfAsync(_requester);

        await actor.TheLastResponse().CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi()).GetParticipantsAsync(group, token));
    }
}
