using System.Globalization;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Support;
using Restaurant_Aqa_Framework.Ui;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>The participants the Group Pre-Order page shows, read panel by panel. Waits until the page has loaded (the
/// owner's own panel is always there). A cart that is not submitted shows no dishes and no subtotal.</summary>
public sealed class TheParticipantsShown : IQuestion<IReadOnlyList<ParticipantView>>
{
    public async Task<IReadOnlyList<ParticipantView>> AnsweredByAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.Participant(ParticipantPanelComponent.OwnersHeading).Root.WaitForAsync();

        var participants = new List<ParticipantView>();
        foreach (var panel in (await page.ParticipantPanels.AllAsync()).Select(root => new ParticipantPanelComponent(root)))
        {
            var heading = (await panel.Heading.InnerTextAsync()).Trim();
            var name = heading == ParticipantPanelComponent.OwnersHeading
                ? CustomerAccount.Owner
                : heading.Replace(ParticipantPanelComponent.GuestHeadingPrefix, "", StringComparison.Ordinal);
            var submitted = (await panel.Status.InnerTextAsync()).Trim() == "Submitted";
            var total = await panel.Subtotal.CountAsync() > 0 ? Money.Parse(await panel.Subtotal.InnerTextAsync()) : (decimal?)null;

            participants.Add(new ParticipantView(name, actor.ByLabel(await ReadLinesAsync(panel)), total, submitted));
        }

        return participants;
    }

    private static async Task<List<(string, int)>> ReadLinesAsync(ParticipantPanelComponent panel)
    {
        var lines = new List<(string, int)>();
        foreach (var line in await panel.Lines.AllAsync())
        {
            var text = (await ParticipantPanelComponent.LineName(line).InnerTextAsync()).Trim();
            var stepper = ParticipantPanelComponent.LineStepperValue(line);
            if (await stepper.CountAsync() > 0)
            {
                lines.Add((text, int.Parse((await stepper.InnerTextAsync()).Trim(), CultureInfo.InvariantCulture)));
                continue;
            }

            var parts = text.Split(" × ");
            lines.Add((parts[0], int.Parse(parts[1], CultureInfo.InvariantCulture)));
        }

        return lines;
    }
}
