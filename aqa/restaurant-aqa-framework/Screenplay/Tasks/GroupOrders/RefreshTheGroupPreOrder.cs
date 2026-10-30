using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>The owner asks the page for the latest carts instead of waiting for its 15-second refresh.</summary>
public sealed class RefreshTheGroupPreOrder : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.RefreshButton.ClickAsync();
        await Assertions.Expect(page.RefreshButton).ToBeEnabledAsync();
    }
}
