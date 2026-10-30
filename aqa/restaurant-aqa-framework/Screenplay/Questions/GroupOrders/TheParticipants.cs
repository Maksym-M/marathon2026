using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>The participants the group service listed: in its last answer (looking at them, or removing one), or —
/// <see cref="RightNow"/> — as the owner would see them now, leaving the last answer alone (to check that a refused
/// change changed nothing).</summary>
public sealed class TheParticipants : IQuestion<IReadOnlyList<ParticipantView>>
{
    private readonly bool _rightNow;

    public TheParticipants()
    {
    }

    private TheParticipants(bool rightNow) => _rightNow = rightNow;

    public static TheParticipants RightNow() => new(rightNow: true);

    public async Task<IReadOnlyList<ParticipantView>> AnsweredByAsync(Actor actor)
    {
        if (!_rightNow)
            return ToViews(actor, actor.TheLastResponse().Json<GroupSnapshotResponse>());

        var owner = actor.Recall<CustomerAccount>(CustomerAccount.Owner);
        var group = actor.Recall<GroupBooking>(GroupBooking.TheGroup);
        var response = await new GroupOrdersApi(actor.UsingTheApi()).GetParticipantsAsync(group.GroupReservationId, owner.IdToken);
        if (!response.Ok)
            throw new InvalidOperationException($"Reading the participants failed with {response.Status}: {await response.ReadErrorMessageAsync()}");

        return ToViews(actor, await response.ReadAsAsync<GroupSnapshotResponse>());
    }

    private static List<ParticipantView> ToViews(Actor actor, GroupSnapshotResponse snapshot) =>
        snapshot.Participants
            .Select(p => new ParticipantView(
                p.Type == "Owner" ? CustomerAccount.Owner : p.Name,
                actor.ByLabel(p.Items.Select(i => (i.Dish.Id, i.Quantity))),
                p.Total,
                p.IsSubmitted))
            .ToList();
}
