using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>The owner presses "Remove Guest" on a guest's panel and confirms; done once the dialog has closed.</summary>
public sealed class RemoveAGuestOnThePage(string guest) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.Participant(ParticipantPanelComponent.GuestHeadingPrefix + guest).RemoveAsync();

        var dialog = page.Dialog("Remove guest?");
        await dialog.ConfirmAsync("Remove Guest");
        await Assertions.Expect(dialog.Root).ToBeHiddenAsync();
    }
}
