using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>Business-level outcomes shared by several features ("the visitor is told ...", "could not be
/// found"). How an outcome shows up on the wire — which status code, which error body — is decided here,
/// so feature files never mention it.</summary>
[Binding]
public sealed class OutcomeStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Then("the visitor is told {string}")]
    [Scope(Tag = "api")]
    public async Task ThenVisitorIsTold(string message)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());

        await Assert.That(outcome.Succeeded).IsFalse().Because($"the request unexpectedly succeeded: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).IsEqualTo(message);
    }

    [Then("sign-in is refused")]
    public async Task ThenSignInIsRefused()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(401).Because($"response body was: {outcome.Body}");
    }

    [Then("the guest is told that the {item} could not be found")]
    public async Task ThenGuestIsToldNotFound(string item)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());

        await Assert.That(outcome.Status).IsEqualTo(404).Because($"response body was: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains("was not found");
    }

    [Then("the location is not created")]
    public async Task ThenLocationIsNotCreated()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsGreaterThanOrEqualTo(400).Because($"the location was created: {outcome.Body}");
    }

    // ---- group pre-order. Someone other than the owner is answered exactly as if the group or reservation did not
    // exist, so nobody can find out which groups exist; someone without an account is asked to sign in. ----

    [Then("the group appears not to exist")]
    public Task ThenTheGroupAppearsNotToExist() => AssertRefusedAsync(404, "Group reservation was not found");

    [Then("the reservation appears not to exist")]
    public Task ThenTheReservationAppearsNotToExist() => AssertRefusedAsync(404, "Reservation not found.");

    [Then("they are asked to sign in")]
    public async Task ThenTheyAreAskedToSignIn()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(401).Because($"response body was: {outcome.Body}");
    }

    /// <summary>A business rule of the group pre-order said no.</summary>
    [Then("the owner is told {string}")]
    public Task ThenTheOwnerIsTold(string message) => AssertRefusedAsync(400, message);

    [Then("{word} is told {string}")]
    public Task ThenGuestIsTold(string guest, string message) => AssertRefusedAsync(400, message);

    [Then("the owner is told the guest could not be found")]
    public async Task ThenTheOwnerIsToldTheGuestCouldNotBeFound()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());

        await Assert.That(outcome.Status).IsEqualTo(404).Because($"response body was: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Matches(@"^Guest session with id \S+ not found\.$");
    }

    [Then("the owner is told their own pre-order could not be found")]
    public async Task ThenTheOwnerIsToldTheirPreOrderCouldNotBeFound()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());

        await Assert.That(outcome.Status).IsEqualTo(400).Because($"response body was: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Matches(@"^Active pre-order for reservation '\S+' not found\.$");
    }

    [Then("{word} is turned away")]
    public Task ThenGuestIsTurnedAway(string guest) => AssertRefusedAsync(401, "Invalid guest session token");

    [Then("the information is available")]
    [Then("the menu is shown")]
    public async Task ThenInformationIsAvailable()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Succeeded).IsTrue().Because($"response status {outcome.Status}, body: {outcome.Body}");
    }

    private async Task AssertRefusedAsync(int status, string message)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());

        await Assert.That(outcome.Status).IsEqualTo(status).Because($"response body was: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).IsEqualTo(message);
    }
}
