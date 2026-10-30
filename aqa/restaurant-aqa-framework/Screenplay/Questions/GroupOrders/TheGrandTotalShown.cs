using System.Globalization;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Support;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>The Grand Total on the Group Pre-Order page — the submitted carts, i.e. what would be sent. Right after a
/// change the backend can still answer with the participants as they were (its list is read from an eventually
/// consistent index), and the page reloads them by itself every 15 s; <see cref="OnceItSettlesOn"/> gives the page
/// that long to show <c>expected</c> before reading whatever it shows.</summary>
public sealed class TheGrandTotalShown : IQuestion<decimal>
{
    private static readonly TimeSpan PageRefreshCycle = TimeSpan.FromSeconds(35);
    private readonly decimal? _expected;

    public TheGrandTotalShown()
    {
    }

    private TheGrandTotalShown(decimal expected) => _expected = expected;

    public static TheGrandTotalShown OnceItSettlesOn(decimal expected) => new(expected);

    public async Task<decimal> AnsweredByAsync(Actor actor)
    {
        var total = new GroupOrderPage(actor.LookingAtThePage()).GrandTotal;
        await total.WaitForAsync();

        if (_expected is { } expected)
        {
            try
            {
                await Assertions.Expect(total).ToHaveTextAsync(
                    expected.ToString("C", CultureInfo.GetCultureInfo("en-US")),
                    new() { Timeout = (float)PageRefreshCycle.TotalMilliseconds });
            }
            catch (PlaywrightException)
            {
                // Not there in time: answer with what the page shows, so the step reports it.
            }
        }

        return Money.Parse(await total.InnerTextAsync());
    }
}
