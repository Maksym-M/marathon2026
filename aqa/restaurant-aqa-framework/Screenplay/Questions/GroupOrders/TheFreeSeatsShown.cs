using Restaurant_Aqa_Framework.Ui;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>How many seats nobody has joined yet the Group Pre-Order page shows.</summary>
public sealed class TheFreeSeatsShown : IQuestion<int>
{
    public async Task<int> AnsweredByAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.Participant(ParticipantPanelComponent.OwnersHeading).Root.WaitForAsync();
        return await page.FreeSeats.CountAsync();
    }
}
