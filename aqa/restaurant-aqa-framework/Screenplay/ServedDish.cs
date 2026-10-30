namespace Restaurant_Aqa_Framework.Screenplay;

/// <summary>A dish the scenario put on the menu, remembered under the short <see cref="Label"/> the feature uses
/// ("Soup"); <see cref="Name"/> is the unique name it really has on the menu.</summary>
public sealed record ServedDish(string Label, string Id, string Name, decimal Price);
