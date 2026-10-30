using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>Whether the Group Pre-Order page told the visitor they have no access to the reservation.</summary>
public sealed class TheNoAccessMessageIsShown : IQuestion<bool>
{
    public async Task<bool> AnsweredByAsync(Actor actor)
    {
        try
        {
            await new GroupOrderPage(actor.LookingAtThePage()).NoAccessMessage.WaitForAsync();
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
