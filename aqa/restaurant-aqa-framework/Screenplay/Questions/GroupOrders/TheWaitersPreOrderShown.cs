using System.Globalization;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>The dishes and quantities on the waiter's pre-order page, by label.</summary>
public sealed class TheWaitersPreOrderShown : IQuestion<IReadOnlyDictionary<string, int>>
{
    public async Task<IReadOnlyDictionary<string, int>> AnsweredByAsync(Actor actor)
    {
        var page = new WaiterPreOrderPage(actor.LookingAtThePage());
        await page.Dishes.First.WaitForAsync();

        var lines = new List<(string, int)>();
        foreach (var dish in await page.Dishes.AllAsync())
        {
            var name = (await WaiterPreOrderPage.DishName(dish).InnerTextAsync()).Trim();
            var quantity = (await WaiterPreOrderPage.DishQuantity(dish).InnerTextAsync()).Replace("Quantity:", "", StringComparison.Ordinal).Trim();
            lines.Add((name, int.Parse(quantity, CultureInfo.InvariantCulture)));
        }

        return actor.ByLabel(lines);
    }
}
