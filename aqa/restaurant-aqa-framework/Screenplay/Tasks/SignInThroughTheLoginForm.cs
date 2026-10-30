using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Signs in through the storefront's login form and waits until the storefront has finished signing in.
/// Leaving the login page is not enough: the storefront moves on as soon as the credentials are accepted, but only
/// keeps the user once its profile request (`/me`) has answered — navigating away before that aborts the request and
/// the next page treats the session as expired. So the task waits for the stored user as well.</summary>
public sealed class SignInThroughTheLoginForm(string email, string password, bool openTheLoginPage = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var loginPage = new LoginPage(page);

        if (openTheLoginPage)
            await loginPage.NavigateAsync();

        await loginPage.SignInAsync(email, password);
        await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.Ordinal));
        await page.WaitForFunctionAsync("() => localStorage.getItem('user') !== null");
    }
}
