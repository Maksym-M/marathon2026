using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.Components;

/// <summary>An `app-modal` confirmation (role "dialog", named by its title): "Submit pre-order?", "Remove guest?".</summary>
public sealed class ConfirmDialogComponent(ILocator root)
{
    public ILocator Root => root;

    public Task ConfirmAsync(string button) => root.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();
}
