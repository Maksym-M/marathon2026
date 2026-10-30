using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>Registers a new customer and signs them in, remembering the account under <paramref name="role"/>
/// (<see cref="CustomerAccount.Owner"/>, <see cref="CustomerAccount.AnotherCustomer"/>). Every scenario gets its
/// own customers, so nothing a scenario books is ever seen by another.</summary>
public sealed class RegisterACustomer(string role) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var visitor = SampleDataFactory.RegisterRequest();
        var api = new AuthApi(actor.UsingTheApi());
        var response = actor.TheLastResponse();

        await response.CaptureRequiredAsync(await api.RegisterAsync(visitor), $"Registering the {role}");
        var session = await response.CaptureRequiredAsync<SignInResponse>(
            await api.SignInAsync(new SignInRequest(visitor.Email, visitor.Password)), $"Signing the {role} in");

        actor.Remember(role, new CustomerAccount(role, visitor.FirstName, visitor.Email, visitor.Password, session.IdToken));
    }
}
