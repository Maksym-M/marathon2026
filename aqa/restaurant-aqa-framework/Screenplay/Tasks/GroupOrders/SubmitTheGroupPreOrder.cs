using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>Someone sends the group's consolidated pre-order to the restaurant — the owner, or anyone else trying to.
/// The factories aim it at what must be refused: a group or reservation that does not exist, or a reservation the
/// group does not belong to. The outcome is the scenario's last response.</summary>
public sealed class SubmitTheGroupPreOrder : ITask
{
    private readonly Requester _requester;
    private readonly Func<Actor, (string Group, string Reservation)> _target;
    private readonly bool _mustSucceed;

    /// <param name="mustSucceed">True when the submission is a precondition ("the owner has submitted …").</param>
    public SubmitTheGroupPreOrder(Requester requester = Requester.Owner, bool mustSucceed = false)
        : this(requester, actor => (Booking(actor).GroupReservationId, Booking(actor).ReservationId), mustSucceed)
    {
    }

    private SubmitTheGroupPreOrder(Requester requester, Func<Actor, (string Group, string Reservation)> target, bool mustSucceed = false)
    {
        _requester = requester;
        _target = target;
        _mustSucceed = mustSucceed;
    }

    public static SubmitTheGroupPreOrder ForAGroupThatDoesNotExist() =>
        new(Requester.Owner, actor => (Guid.NewGuid().ToString(), Booking(actor).ReservationId));

    public static SubmitTheGroupPreOrder ForAReservationThatDoesNotExist() =>
        new(Requester.Owner, actor => (Booking(actor).GroupReservationId, Guid.NewGuid().ToString()));

    public static SubmitTheGroupPreOrder WithTheReservationOfAnotherGroup() =>
        new(Requester.Owner, actor => (Booking(actor).GroupReservationId, Booking(actor, GroupBooking.AnotherGroup).ReservationId));

    public async Task PerformAsAsync(Actor actor)
    {
        var (group, reservation) = _target(actor);
        var token = await actor.TokenOfAsync(_requester);

        var call = await new GroupOrdersApi(actor.UsingTheApi()).SubmitAsync(group, reservation, token);

        if (_mustSucceed)
            await actor.TheLastResponse().CaptureRequiredAsync(call, "Submitting the group pre-order");
        else
            await actor.TheLastResponse().CaptureAsync(call);
    }

    private static GroupBooking Booking(Actor actor, string group = GroupBooking.TheGroup) => actor.Recall<GroupBooking>(group);
}
