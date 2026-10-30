using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.Components;

/// <summary>`app-dish-summary`: one line per dish, "{name}" and "× {quantity}", most ordered first.</summary>
public sealed class DishSummaryComponent(ILocator root)
{
    public ILocator Lines => root.Locator("li.line");

    public static ILocator LineName(ILocator line) => line.Locator(".line-name");
    public static ILocator LineQuantity(ILocator line) => line.Locator(".line-quantity");
}
