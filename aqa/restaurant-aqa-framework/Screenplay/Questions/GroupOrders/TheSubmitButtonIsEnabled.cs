using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>Whether the owner can press "Submit Pre-Order" on the page.</summary>
public sealed class TheSubmitButtonIsEnabled : IQuestion<bool>
{
    public async Task<bool> AnsweredByAsync(Actor actor)
    {
        var button = new GroupOrderPage(actor.LookingAtThePage()).SubmitButton;
        await button.WaitForAsync();
        return await button.IsEnabledAsync();
    }
}
