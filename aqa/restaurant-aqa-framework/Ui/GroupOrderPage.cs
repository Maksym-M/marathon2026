using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>The owner's Group Pre-Order page (`/reservations/{id}/group-order`): one panel per participant (the owner's
/// "My Pre-Order" first, then "Guest: {name}"), a placeholder per seat nobody has joined, and the Grand Total card with
/// the Submit button. The page polls every 15 s; "Refresh" reloads on demand. A guest's dishes and subtotal are only
/// rendered once their cart is submitted.</summary>
public sealed class GroupOrderPage(IPage page) : BasePage(page)
{
    public ILocator Heading => Page.GetByRole(AriaRole.Heading, new() { Name = "Group Pre-Order", Level = 1 });
    public ILocator StatusBadge => Page.Locator(".title-row .badge");
    public ILocator NoAccessMessage => Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "You don't have access to this reservation." });

    /// <summary>The `section` labelled by its "Participants" heading — exposed as a region.</summary>
    private ILocator ParticipantsSection => Page.GetByRole(AriaRole.Region, new() { Name = "Participants" });
    public ILocator ParticipantPanels => ParticipantsSection.Locator("article.panel");
    public ILocator FreeSeats => ParticipantsSection.GetByRole(AriaRole.Article, new() { Name = "Seat nobody has joined yet" });
    public ILocator RefreshButton => Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true });

    private ILocator GrandTotalCard => Page.GetByRole(AriaRole.Region, new() { Name = "Grand Total" });
    public ILocator GrandTotal => GrandTotalCard.Locator(".total-value");
    public ILocator SubmitButton => GrandTotalCard.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order" });
    public ILocator SentToRestaurant => GrandTotalCard.GetByRole(AriaRole.Status).Filter(new() { HasText = "Sent to Restaurant" });

    /// <summary>The merged dishes shown once the pre-order has been sent.</summary>
    public DishSummaryComponent DishSummary => new(Page.Locator("app-dish-summary"));

    public Task NavigateAsync(string reservationId) => NavigateToAsync($"/reservations/{reservationId}/group-order");

    public ParticipantPanelComponent Participant(string heading) =>
        new(ParticipantPanels.Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) }));

    public ConfirmDialogComponent Dialog(string title) => new(Page.GetByRole(AriaRole.Dialog, new() { Name = title }));
}
