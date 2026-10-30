using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>The waiter's view of one reservation's pre-order (`/waiter-pre-order`): a dish card per line with
/// "Quantity: {n}" and Confirm/Cancel buttons.</summary>
public sealed class WaiterPreOrderPage(IPage page) : BasePage(page)
{
    public ILocator Title => Page.GetByRole(AriaRole.Heading, new() { NameRegex = new("^Pre-order of ") });
    public ILocator Dishes => Page.Locator("app-dish-card.pre-order-dish");

    public static ILocator DishName(ILocator dish) => dish.Locator(".dish-name");
    public static ILocator DishQuantity(ILocator dish) => dish.Locator(".pre-order-quantity");
}
