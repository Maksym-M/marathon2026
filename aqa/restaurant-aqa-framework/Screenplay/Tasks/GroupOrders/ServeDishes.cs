using System.Text.Json;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>Makes sure the waiter's location serves dishes with these labels and prices, so totals are exact and never
/// depend on the real menu. The dishes are shared by the whole run (see <see cref="RunScopedDishes"/> for why); each
/// scenario only remembers them under their labels. The location's menu is read from an eventually consistent
/// index (guests are refused a dish "not available at this location" for a moment after it is created), so a new
/// dish is only handed out once the menu lists it.</summary>
public sealed class ServeDishes(IReadOnlyList<(string Label, decimal Price)> dishes) : ITask
{
    private static readonly TimeSpan MenuPropagationTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MenuPollInterval = TimeSpan.FromSeconds(1);

    public async Task PerformAsAsync(Actor actor)
    {
        var location = await actor.AsksForAsync(new TheWaitersLocation());

        foreach (var (label, price) in dishes)
        {
            var dish = await RunScopedDishes.GetOrCreateAsync(label, price, () => CreateAsync(actor, location.LocationId, label, price));
            actor.Remember(label, new ServedDish(label, dish.Id, dish.Name, dish.Price));
        }
    }

    private static async Task<DishResponse> CreateAsync(Actor actor, string locationId, string label, decimal price)
    {
        var request = SampleDataFactory.CreateDishRequest([locationId]) with
        {
            Name = $"{SampleDataFactory.Prefix}{label}-{Guid.NewGuid().ToString("N")[..6]}",
            Price = price,
        };
        var dish = await actor.TheLastResponse().CaptureRequiredAsync<DishResponse>(
            await new DishesApi(actor.UsingTheApi()).CreateDishAsync(request), $"Serving {label}");

        await WaitUntilTheMenuListsAsync(actor, locationId, dish.Id);
        return dish;
    }

    private static async Task WaitUntilTheMenuListsAsync(Actor actor, string locationId, string dishId)
    {
        var locations = new LocationsApi(actor.UsingTheApi());
        var deadline = DateTime.UtcNow.Add(MenuPropagationTimeout);
        while (true)
        {
            var response = await locations.GetDishesForLocationAsync(locationId);
            if (response.Ok && (await response.ReadAsAsync<List<JsonElement>>()).Any(d => d.GetProperty("id").GetString() == dishId))
                return;

            if (DateTime.UtcNow > deadline)
                throw new InvalidOperationException($"The location's menu still does not list dish {dishId} after {MenuPropagationTimeout.TotalSeconds}s.");

            await Task.Delay(MenuPollInterval);
        }
    }
}
