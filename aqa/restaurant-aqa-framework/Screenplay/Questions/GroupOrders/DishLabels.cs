namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>Turns the unique names the scenario's dishes really have back into the labels its feature uses.</summary>
public static class DishLabels
{
    public static string LabelOfDish(this Actor actor, string idOrName) =>
        actor.RecallAll<ServedDish>().FirstOrDefault(d => d.Id == idOrName || d.Name == idOrName)?.Label ?? idOrName;

    public static IReadOnlyDictionary<string, int> ByLabel(this Actor actor, IEnumerable<(string IdOrName, int Quantity)> lines) =>
        lines.GroupBy(line => actor.LabelOfDish(line.IdOrName)).ToDictionary(g => g.Key, g => g.Sum(line => line.Quantity));
}
