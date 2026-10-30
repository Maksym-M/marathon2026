using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.Components;

/// <summary>One participant's panel on the Group Pre-Order page. The heading is "My Pre-Order" for the owner and
/// "Guest: {name}" for a guest; the status reads "Submitted" or "Not submitted yet" (a cart counts as submitted only
/// with dishes in it, so an owner who ordered nothing reads "Not submitted yet"). Lines and the subtotal exist only
/// for a submitted cart. A line the owner may edit shows the quantity in a stepper, otherwise as "{name} × {n}".</summary>
public sealed class ParticipantPanelComponent(ILocator root)
{
    public const string OwnersHeading = "My Pre-Order";
    public const string GuestHeadingPrefix = "Guest: ";

    public ILocator Root => root;
    public ILocator Heading => root.GetByRole(AriaRole.Heading, new() { Level = 3 });
    public ILocator Status => root.Locator(".status");
    public ILocator Lines => root.Locator("li.line");
    public ILocator Subtotal => root.Locator(".subtotal span").Last;
    public ILocator RemoveButton => root.GetByRole(AriaRole.Button, new() { NameRegex = new("^Remove guest ") });

    public static ILocator LineName(ILocator line) => line.Locator(".line-name");

    /// <summary>The stepper's value on an editable line; absent on a read-only one.</summary>
    public static ILocator LineStepperValue(ILocator line) => line.Locator(".stepper .value");

    public Task RemoveAsync() => RemoveButton.ClickAsync();
}
