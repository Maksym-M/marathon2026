using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>The waiter's dashboard (`/waiter-reservations`): every reservation of the waiter's location as an
/// `app-card-reservation`, unfiltered until a filter is applied. A card with a pre-order has a "Pre-order" button.</summary>
public sealed class WaiterReservationsPage(IPage page) : BasePage(page)
{
    public Task NavigateAsync() => NavigateToAsync("/waiter-reservations");

    /// <summary>The card of the reservation <paramref name="customer"/> made at table <paramref name="tableNumber"/> that has a
    /// pre-order. Tables are shared by many reservations, so the customer who booked it (named on the card) tells it apart.</summary>
    public ILocator Reservation(string customer, int tableNumber) =>
        Page.Locator("app-card-reservation")
            .Filter(new() { HasText = customer })
            .Filter(new() { HasTextRegex = new Regex($@"\bTable {tableNumber}\b") })
            .Filter(new() { Has = Page.GetByRole(AriaRole.Button, new() { Name = "Pre-order", Exact = true }) });

    public Task OpenPreOrderAsync(string customer, int tableNumber) =>
        Reservation(customer, tableNumber).GetByRole(AriaRole.Button, new() { Name = "Pre-order", Exact = true }).ClickAsync();
}
