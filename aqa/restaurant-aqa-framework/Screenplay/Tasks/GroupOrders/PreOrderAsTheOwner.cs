using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>The owner starts their own pre-order for the group's reservation, as they would from the menu. It is the
/// order the guests' carts are merged into when the group is submitted.</summary>
public sealed class PreOrderAsTheOwner(IReadOnlyList<(string Dish, int Quantity)> lines) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var owner = actor.Recall<CustomerAccount>(CustomerAccount.Owner);
        var booking = actor.Recall<GroupBooking>(GroupBooking.TheGroup);
        var items = lines.Select(line => new OrderItemRequest(actor.Recall<ServedDish>(line.Dish).Id, line.Quantity)).ToList();

        await actor.TheLastResponse().CaptureRequiredAsync(
            await new OrdersApi(actor.UsingTheApi()).CreateOrderAsync(new CreateOrderRequest(booking.ReservationId, items), owner.IdToken),
            "The owner pre-ordering");
    }
}
