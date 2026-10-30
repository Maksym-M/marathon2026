using Restaurant_Aqa_Framework.Api.Dtos.Dishes;

namespace Restaurant_Aqa_Framework.Api.Seeding;

/// <summary>
/// Dishes the group pre-order scenarios order from: created once per test run for each (label, price) a feature
/// asks for, then shared read-only by every scenario — none of them changes a dish. Run-scoped on purpose:
/// the backend adds a dish to a location by rewriting the location's whole dish list without a version check
/// (DishService.CreateDishAsync / DeleteDishAsync), so two scenarios creating dishes at the same location at the same
/// moment silently lose one of them from the menu. Creating them one at a time, once, keeps that defect out of
/// unrelated scenarios. They carry the <see cref="SampleDataFactory.Prefix"/>, so the run-level sweep in
/// <c>PreRunCleanupHooks</c> removes them, one at a time, after the last scenario.
/// </summary>
public static class RunScopedDishes
{
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);
    private static readonly Dictionary<(string Label, decimal Price), DishResponse> Served = [];

    public static async Task<DishResponse> GetOrCreateAsync(string label, decimal price, Func<Task<DishResponse>> create)
    {
        await OneAtATime.WaitAsync();
        try
        {
            if (!Served.TryGetValue((label, price), out var dish))
            {
                dish = await create();
                Served[(label, price)] = dish;
            }

            return dish;
        }
        finally
        {
            OneAtATime.Release();
        }
    }
}
