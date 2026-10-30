using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>The pre-order of the group's reservation, as the owner or the waiter reads it from the service. Leaves the
/// scenario's last response alone.</summary>
public sealed class ThePreOrderOfTheReservation(Requester requester = Requester.Owner) : IQuestion<PreOrderView>
{
    public async Task<PreOrderView> AnsweredByAsync(Actor actor)
    {
        var booking = actor.Recall<GroupBooking>(GroupBooking.TheGroup);
        var response = await new OrdersApi(actor.UsingTheApi()).GetOrderAsync(booking.ReservationId, await actor.TokenOfAsync(requester));
        if (!response.Ok)
            throw new InvalidOperationException($"Reading the pre-order as {requester} failed with {response.Status}: {await response.ReadErrorMessageAsync()}");

        var order = await response.ReadAsAsync<GetOrderResponse>();
        return new PreOrderView(order.Status, actor.ByLabel(order.OrderItems.Select(i => (i.Dish.Id, i.Quantity))));
    }
}
