using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>The bearer token a <see cref="Requester"/> calls the group routes with — whoever is needed is set up the
/// first time (another customer registers, a guest joins). A visitor who is not signed in has none.</summary>
public static class Requesters
{
    private const string StandInGuest = "Gina";

    public static async Task<string?> TokenOfAsync(this Actor actor, Requester requester)
    {
        switch (requester)
        {
            case Requester.Owner:
                return actor.Recall<CustomerAccount>(CustomerAccount.Owner).IdToken;
            case Requester.AnotherCustomer:
                if (actor.RecallAll<CustomerAccount>().FirstOrDefault(a => a.Role == CustomerAccount.AnotherCustomer) is not { } other)
                {
                    await actor.AttemptsToAsync(new RegisterACustomer(CustomerAccount.AnotherCustomer));
                    other = actor.Recall<CustomerAccount>(CustomerAccount.AnotherCustomer);
                }

                return other.IdToken;
            case Requester.Waiter:
                return (await actor.AsksForAsync(new TheWaiterSession())).IdToken;
            case Requester.GuestOfTheGroup:
                var group = actor.Recall<GroupBooking>(GroupBooking.TheGroup).GroupReservationId;
                if (actor.RecallAll<GuestSession>().FirstOrDefault(g => g.GroupReservationId == group) is not { } guest)
                {
                    await actor.AttemptsToAsync(new JoinTheGroup(StandInGuest));
                    guest = actor.Recall<GuestSession>(StandInGuest);
                }

                return guest.Token;
            case Requester.Visitor:
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(requester), requester, null);
        }
    }
}
