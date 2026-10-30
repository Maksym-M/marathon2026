using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>Where the group pre-order stands ("Open", "Submitted", "Closed"), as the owner sees it. Leaves the
/// scenario's last response alone.</summary>
public sealed class TheGroupStatus : IQuestion<string>
{
    public async Task<string> AnsweredByAsync(Actor actor)
    {
        var owner = actor.Recall<CustomerAccount>(CustomerAccount.Owner);
        var group = actor.Recall<GroupBooking>(GroupBooking.TheGroup);
        var response = await new GroupOrdersApi(actor.UsingTheApi()).GetStatusAsync(group.GroupReservationId, owner.IdToken);
        if (!response.Ok)
            throw new InvalidOperationException($"Reading the group status failed with {response.Status}: {await response.ReadErrorMessageAsync()}");

        return (await response.ReadAsAsync<GroupStatusResponse>()).Status;
    }
}
