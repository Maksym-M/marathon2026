namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>The owner starts their own pre-order and takes the dish out again, so the pre-order exists but holds
/// nothing — the case where the owner adds nothing of their own to the group.</summary>
public sealed class StartAnEmptyPreOrderAsTheOwner : ITask
{
    public Task PerformAsAsync(Actor actor)
    {
        var dish = actor.RecallAll<ServedDish>()[0].Label;
        return actor.AttemptsToAsync(new PreOrderAsTheOwner([(dish, 1)]), new ChangeACart(CustomerAccount.Owner, dish, 0));
    }
}
