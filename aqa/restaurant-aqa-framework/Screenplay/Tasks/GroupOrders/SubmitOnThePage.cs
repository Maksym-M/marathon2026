using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>The owner presses "Submit Pre-Order" and confirms, then waits for the page to say it was sent.</summary>
public sealed class SubmitOnThePage : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.SubmitButton.ClickAsync();
        await page.Dialog("Submit pre-order?").ConfirmAsync("Submit");
        await Assertions.Expect(page.SentToRestaurant).ToBeVisibleAsync();
    }
}
