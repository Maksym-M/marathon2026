using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;
using Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>The group pre-order: an owner's booking, the guests who join it through the invite link and build their
/// own carts, and the owner who reviews them, removes guests and sends one consolidated pre-order to the restaurant.
/// Guests are named in the feature ("Anna"); everyone else is a <see cref="Requester"/>. What a refusal looks like
/// on the wire is decided in <see cref="OutcomeStepDefinitions"/>.</summary>
[Binding]
public sealed class GroupPreOrderStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    // ---- the booking, the menu and the guests ----

    [Given("the restaurant serves:")]
    public Task GivenTheRestaurantServes(DishPrices dishes) => Actor.AttemptsToAsync(new ServeDishes(dishes));

    [Given("a customer has booked a table for {int}")]
    public Task GivenACustomerHasBookedATableFor(int seats) =>
        Actor.AttemptsToAsync(new RegisterACustomer(CustomerAccount.Owner), new BookAGroupTable(seats));

    [Given("the owner has another booking, for {int}")]
    public Task GivenTheOwnerHasAnotherBooking(int seats) =>
        Actor.AttemptsToAsync(new BookAGroupTable(seats, GroupBooking.AnotherGroup));

    [Given("{word} has joined the group")]
    public Task GivenGuestHasJoined(string guest) => Actor.AttemptsToAsync(new JoinTheGroup(guest));

    [Given("{word} has joined the other booking's group")]
    public Task GivenGuestHasJoinedTheOtherGroup(string guest) =>
        Actor.AttemptsToAsync(new JoinTheGroup(guest, GroupBooking.AnotherGroup));

    [Given("{word} has joined the group and added:")]
    public Task GivenGuestHasJoinedAndAdded(string guest, DishQuantities dishes) =>
        Actor.AttemptsToAsync([new JoinTheGroup(guest), .. AddAll(guest, dishes)]);

    [Given("{word} has joined the group and submitted:")]
    public Task GivenGuestHasJoinedAndSubmitted(string guest, DishQuantities dishes) =>
        Actor.AttemptsToAsync([new JoinTheGroup(guest), .. AddAll(guest, dishes), new SubmitTheGuestCart(guest)]);

    [When("{word} adds {int} {word} to their cart")]
    public Task WhenGuestAdds(string guest, int quantity, string dish) =>
        Actor.AttemptsToAsync(new AddToTheGuestCart(guest, dish, quantity));

    [Given("{word} has submitted their cart")]
    [When("{word} submits their cart")]
    public Task WhenGuestSubmits(string guest) => Actor.AttemptsToAsync(new SubmitTheGuestCart(guest));

    [When("{word} tries to submit their empty cart")]
    public Task WhenGuestTriesToSubmitAnEmptyCart(string guest) =>
        Actor.AttemptsToAsync(new SubmitTheGuestCart(guest, mustSucceed: false));

    [When("{word} goes back to the group with their invite")]
    public Task WhenGuestGoesBack(string guest) => Actor.AttemptsToAsync(new UseTheInvite(guest));

    // ---- the owner's own pre-order and changes to carts ----

    [Given("the owner has pre-ordered:")]
    public Task GivenTheOwnerHasPreOrdered(DishQuantities dishes) => Actor.AttemptsToAsync(new PreOrderAsTheOwner(dishes));

    [Given("the owner has started a pre-order and taken everything out of it again")]
    public Task GivenTheOwnersPreOrderIsEmpty() => Actor.AttemptsToAsync(new StartAnEmptyPreOrderAsTheOwner());

    [Given("the owner has taken the {word} out of {word}'s cart")]
    public Task GivenTheOwnerHasTakenADishOut(string dish, string guest) => Actor.AttemptsToAsync(new ChangeACart(guest, dish, 0));

    [Given("the owner has opened the group pre-order")]
    [Scope(Tag = "api")]
    public Task GivenTheOwnerHasOpenedTheGroupPreOrder() => Actor.AttemptsToAsync(new TakeAPlaceInTheGroup());

    // ---- looking at the participants ----

    [When("{requester} looks at the group's participants")]
    public Task WhenSomeoneLooksAtTheParticipants(Requester requester) => Actor.AttemptsToAsync(new LookAtTheParticipants(requester));

    [When("the owner looks at the participants of a group that does not exist")]
    public Task WhenTheOwnerLooksAtAMissingGroup() => Actor.AttemptsToAsync(LookAtTheParticipants.OfAGroupThatDoesNotExist());

    [Then("the participants are:")]
    [Scope(Tag = "api")]
    public async Task ThenTheParticipantsAre(DataTable expected)
    {
        var participants = await Actor.AsksForAsync(new TheParticipants());
        await Assert.That(ParticipantsTable.Mismatches(expected, participants)).IsEmpty()
            .Because($"the service listed: {ParticipantsTable.Describe(participants)}");
    }

    [Then("the total of all carts is {decimal}")]
    public async Task ThenTheTotalOfAllCartsIs(decimal total) =>
        await Assert.That(await Actor.AsksForAsync(new TheTotalOfAllCarts())).IsEqualTo(total);

    [Then("only the owner is listed, with nothing ordered")]
    [Scope(Tag = "api")]
    public async Task ThenOnlyTheOwnerIsListed()
    {
        await AssertOnlyTheOwnerAsync(await Actor.AsksForAsync(new TheParticipants()), "the service");
        await Assert.That(await Actor.AsksForAsync(new TheTotalOfAllCarts())).IsEqualTo(0m);
    }

    [Then("{word} is still in the group")]
    public async Task ThenGuestIsStillInTheGroup(string guest)
    {
        var participants = await Actor.AsksForAsync(TheParticipants.RightNow());
        await Assert.That(participants.Select(p => p.Name)).Contains(guest).Because($"the service listed: {ParticipantsTable.Describe(participants)}");
    }

    // ---- submitting ----

    [When("{requester} submits the group pre-order")]
    [Scope(Tag = "api")]
    public Task WhenSomeoneSubmits(Requester requester) => Actor.AttemptsToAsync(new SubmitTheGroupPreOrder(requester));

    [Given("the owner has submitted the group pre-order")]
    public Task GivenTheOwnerHasSubmitted() => Actor.AttemptsToAsync(new SubmitTheGroupPreOrder(mustSucceed: true));

    [When("the owner submits the pre-order of a group that does not exist")]
    public Task WhenTheOwnerSubmitsAMissingGroup() => Actor.AttemptsToAsync(SubmitTheGroupPreOrder.ForAGroupThatDoesNotExist());

    [When("the owner submits the group pre-order for a reservation that does not exist")]
    public Task WhenTheOwnerSubmitsForAMissingReservation() =>
        Actor.AttemptsToAsync(SubmitTheGroupPreOrder.ForAReservationThatDoesNotExist());

    [When("the owner submits the group pre-order with the reservation of the other booking")]
    public Task WhenTheOwnerSubmitsWithTheOtherReservation() =>
        Actor.AttemptsToAsync(SubmitTheGroupPreOrder.WithTheReservationOfAnotherGroup());

    [Then("the restaurant receives the pre-order:")]
    public async Task ThenTheRestaurantReceives(DishQuantities expected)
    {
        var preOrder = await Actor.AsksForAsync(new ThePreOrderOfTheReservation());

        var received = DishList.Format(preOrder.Dishes);

        await Assert.That(preOrder.Status).IsEqualTo(OrderStatus.SubmittedByCustomer).Because($"the restaurant received: {received}");
        await Assert.That(received).IsEqualTo(DishList.Format(expected.ByDish())).Because($"the restaurant received: {received}");
    }

    [Then("the group pre-order is marked as submitted")]
    public async Task ThenTheGroupIsSubmitted() =>
        await Assert.That(await Actor.AsksForAsync(new TheGroupStatus())).IsEqualTo("Submitted");

    [Then("the waiter sees the pre-order:")]
    [Scope(Tag = "api")]
    public async Task ThenTheWaiterSees(DishQuantities expected)
    {
        var preOrder = await Actor.AsksForAsync(new ThePreOrderOfTheReservation(Requester.Waiter));
        var seen = DishList.Format(preOrder.Dishes);
        await Assert.That(seen).IsEqualTo(DishList.Format(expected.ByDish())).Because($"the waiter was given: {seen}");
    }

    // ---- removing participants ----

    [When("{requester} removes {word} from the group")]
    [Scope(Tag = "api")]
    public Task WhenSomeoneRemovesAGuest(Requester requester, string guest) =>
        Actor.AttemptsToAsync(new RemoveAParticipant(guest, requester));

    [Given("the owner has removed {word} from the group")]
    public Task GivenTheOwnerHasRemoved(string guest) => Actor.AttemptsToAsync(new RemoveAParticipant(guest, mustSucceed: true));

    [When("the owner removes a guest who never joined")]
    public Task WhenTheOwnerRemovesAGuestWhoNeverJoined() => Actor.AttemptsToAsync(RemoveAParticipant.WhoNeverJoined());

    [When("the owner tries to remove their own place in the group")]
    public Task WhenTheOwnerRemovesTheirOwnPlace() => Actor.AttemptsToAsync(RemoveAParticipant.TheOwnersOwnPlace());

    // ---- on the storefront ----

    [When("the owner opens the group pre-order")]
    [Scope(Tag = "ui")]
    public Task WhenTheOwnerOpensTheGroupPreOrder() => Actor.AttemptsToAsync(new OpenTheGroupPreOrder());

    [When("another customer opens the owner's group pre-order")]
    public Task WhenAnotherCustomerOpensIt() => Actor.AttemptsToAsync(new OpenTheGroupPreOrder(Requester.AnotherCustomer));

    [When("the owner refreshes the group pre-order")]
    public Task WhenTheOwnerRefreshes() => Actor.AttemptsToAsync(new RefreshTheGroupPreOrder());

    [When("the owner submits the group pre-order")]
    [Scope(Tag = "ui")]
    public Task WhenTheOwnerSubmitsOnThePage() => Actor.AttemptsToAsync(new SubmitOnThePage());

    [When("the owner removes {word} from the group")]
    [Scope(Tag = "ui")]
    public Task WhenTheOwnerRemovesOnThePage(string guest) => Actor.AttemptsToAsync(new RemoveAGuestOnThePage(guest));

    [When("the waiter opens the reservation's pre-order")]
    public Task WhenTheWaiterOpensThePreOrder() => Actor.AttemptsToAsync(new OpenTheReservationsPreOrderAsTheWaiter());

    [Then("the participants are:")]
    [Scope(Tag = "ui")]
    public async Task ThenThePageShowsTheParticipants(DataTable expected)
    {
        var participants = await Actor.AsksForAsync(new TheParticipantsShown());
        await Assert.That(ParticipantsTable.Mismatches(expected, participants)).IsEmpty()
            .Because($"the page showed: {ParticipantsTable.Describe(participants)}");
    }

    [Then("only the owner is listed, with nothing ordered")]
    [Scope(Tag = "ui")]
    public async Task ThenThePageListsOnlyTheOwner()
    {
        await AssertOnlyTheOwnerAsync(await Actor.AsksForAsync(new TheParticipantsShown()), "the page");
        await Assert.That(await Actor.AsksForAsync(new TheGrandTotalShown())).IsEqualTo(0m);
    }

    [Then("the grand total shown is {decimal}")]
    public async Task ThenTheGrandTotalShownIs(decimal total) =>
        await Assert.That(await Actor.AsksForAsync(new TheGrandTotalShown())).IsEqualTo(total);

    [Then("the grand total shown becomes {decimal}")]
    public async Task ThenTheGrandTotalShownBecomes(decimal total) =>
        await Assert.That(await Actor.AsksForAsync(TheGrandTotalShown.OnceItSettlesOn(total))).IsEqualTo(total);

    [Then("{int} seats are still free")]
    public async Task ThenSeatsAreStillFree(int seats) =>
        await Assert.That(await Actor.AsksForAsync(new TheFreeSeatsShown())).IsEqualTo(seats);

    [Then("the owner cannot submit the pre-order yet")]
    public async Task ThenTheOwnerCannotSubmitYet() =>
        await Assert.That(await Actor.AsksForAsync(new TheSubmitButtonIsEnabled())).IsFalse();

    [Then("the owner sees that the pre-order was sent to the restaurant with:")]
    public async Task ThenTheOwnerSeesItWasSent(DishQuantities expected)
    {
        var (badge, dishes) = await Actor.AsksForAsync(new TheSentPreOrderShown());
        var shown = DishList.Format(dishes);

        await Assert.That(badge).IsEqualTo("Sent to Restaurant");
        await Assert.That(shown).IsEqualTo(DishList.Format(expected.ByDish())).Because($"the page showed: {shown}");
    }

    [Then("the waiter sees the pre-order:")]
    [Scope(Tag = "ui")]
    public async Task ThenTheWaiterSeesOnThePage(DishQuantities expected)
    {
        var shown = DishList.Format(await Actor.AsksForAsync(new TheWaitersPreOrderShown()));
        await Assert.That(shown).IsEqualTo(DishList.Format(expected.ByDish())).Because($"the waiter's page showed: {shown}");
    }

    [Then("another customer is told they have no access")]
    public async Task ThenAnotherCustomerHasNoAccess() =>
        await Assert.That(await Actor.AsksForAsync(new TheNoAccessMessageIsShown())).IsTrue();

    private static async Task AssertOnlyTheOwnerAsync(IReadOnlyList<ParticipantView> participants, string source)
    {
        var described = $"{source} listed: {ParticipantsTable.Describe(participants)}";
        await Assert.That(participants.Select(p => p.Name)).IsEquivalentTo([CustomerAccount.Owner]).Because(described);
        await Assert.That(participants[0].Dishes).IsEmpty().Because(described);
    }

    private static IEnumerable<ITask> AddAll(string guest, DishQuantities dishes) =>
        dishes.Select(line => new AddToTheGuestCart(guest, line.Dish, line.Quantity));
}
