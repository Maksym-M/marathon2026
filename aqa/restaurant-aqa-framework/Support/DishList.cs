using System.Globalization;

namespace Restaurant_Aqa_Framework.Support;

/// <summary>Dishes and how many of each, written the way a person reads a cart: "Soup × 2, Steak × 1" ("none" when
/// empty). Order does not matter — two lists are equal when they hold the same dishes in the same quantities.</summary>
public static class DishList
{
    public const string None = "none";

    public static IReadOnlyDictionary<string, int> Parse(string text) =>
        string.Equals(text.Trim(), None, StringComparison.OrdinalIgnoreCase)
            ? new Dictionary<string, int>()
            : text.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split("×", StringSplitOptions.TrimEntries))
                .ToDictionary(parts => parts[0], parts => int.Parse(parts[1], CultureInfo.InvariantCulture));

    public static string Format(IReadOnlyDictionary<string, int> dishes) =>
        dishes.Count == 0
            ? None
            : string.Join(", ", dishes.OrderBy(d => d.Key, StringComparer.Ordinal).Select(d => $"{d.Key} × {d.Value}"));

    public static bool SameAs(this IReadOnlyDictionary<string, int> dishes, IReadOnlyDictionary<string, int> other) =>
        Format(dishes) == Format(other);
}
