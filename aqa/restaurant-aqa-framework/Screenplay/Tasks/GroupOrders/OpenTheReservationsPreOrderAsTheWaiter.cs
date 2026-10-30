using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>The waiter takes over the browser (whoever was signed in is signed out — the storefront keeps its session in
/// browser storage), finds the group's reservation on the dashboard by its owner and table, and opens its pre-order.</summary>
public sealed class OpenTheReservationsPreOrderAsTheWaiter : ITask
{
    /// <summary>The dashboard loads every reservation of the location and then three more requests per reservation
    /// (feedback, order, customer name). With the bookings a test run leaves behind and the environment's 10
    /// concurrent Lambdas, that takes 20–40 s — a performance defect of its own, reported separately. The card is
    /// still waited for, just with the time the page really needs instead of the 15 s default.</summary>
    private const float DashboardLoadTimeoutMs = 60_000;

    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        await page.Context.ClearCookiesAsync();
        // A browser that has not opened the storefront yet (about:blank) has no storage to clear, and refuses access to it.
        if (page.Url.StartsWith("http", StringComparison.Ordinal))
            await page.EvaluateAsync("() => { localStorage.clear(); sessionStorage.clear(); }");

        var (username, password) = Settings.Current.RequireWaiter();
        await actor.AttemptsToAsync(new SignInThroughTheLoginForm(username, password));

        var dashboard = new WaiterReservationsPage(page);
        await dashboard.NavigateAsync();

        var owner = actor.Recall<CustomerAccount>(CustomerAccount.Owner).FirstName;
        var table = actor.Recall<GroupBooking>(GroupBooking.TheGroup).TableNumber;
        await dashboard.Reservation(owner, table).WaitForAsync(new() { Timeout = DashboardLoadTimeoutMs });
        await dashboard.OpenPreOrderAsync(owner, table);
        await Assertions.Expect(new WaiterPreOrderPage(page).Title).ToBeVisibleAsync();
    }
}
