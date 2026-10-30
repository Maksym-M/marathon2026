using System.Globalization;
using Restaurant_Aqa_Framework.Ui;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>What the Group Pre-Order page shows once the pre-order went to the restaurant: the page's badge, and the
/// merged dishes of the Dish Summary by label. Waits for "Sent to Restaurant" first.</summary>
public sealed class TheSentPreOrderShown : IQuestion<(string Badge, IReadOnlyDictionary<string, int> Dishes)>
{
    public async Task<(string Badge, IReadOnlyDictionary<string, int> Dishes)> AnsweredByAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.SentToRestaurant.WaitForAsync();
        await page.DishSummary.Lines.First.WaitForAsync();

        var lines = new List<(string, int)>();
        foreach (var line in await page.DishSummary.Lines.AllAsync())
        {
            var name = (await DishSummaryComponent.LineName(line).InnerTextAsync()).Trim();
            var quantity = (await DishSummaryComponent.LineQuantity(line).InnerTextAsync()).Replace("×", "", StringComparison.Ordinal).Trim();
            lines.Add((name, int.Parse(quantity, CultureInfo.InvariantCulture)));
        }

        return ((await page.StatusBadge.InnerTextAsync()).Trim(), actor.ByLabel(lines));
    }
}
