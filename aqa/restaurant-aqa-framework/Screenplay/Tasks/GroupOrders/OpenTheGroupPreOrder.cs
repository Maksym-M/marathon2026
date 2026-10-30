using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>A customer signs in on the storefront and opens the group's pre-order page — the owner, or another customer
/// trying to open someone else's.</summary>
public sealed class OpenTheGroupPreOrder(Requester requester = Requester.Owner) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        await actor.TokenOfAsync(requester);
        var account = requester switch
        {
            Requester.Owner => actor.Recall<CustomerAccount>(CustomerAccount.Owner),
            Requester.AnotherCustomer => actor.Recall<CustomerAccount>(CustomerAccount.AnotherCustomer),
            _ => throw new NotSupportedException($"Only a customer opens a group pre-order page, not {requester}."),
        };

        await actor.AttemptsToAsync(new SignInThroughTheLoginForm(account.Email, account.Password));
        await new GroupOrderPage(actor.LookingAtThePage()).NavigateAsync(actor.Recall<GroupBooking>(GroupBooking.TheGroup).ReservationId);
    }
}
