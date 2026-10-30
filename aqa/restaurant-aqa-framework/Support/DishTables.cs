using System.Globalization;

namespace Restaurant_Aqa_Framework.Support;

/// <summary>A `| dish | quantity |` table: what someone orders, or what the restaurant receives.</summary>
public sealed class DishQuantities : List<(string Dish, int Quantity)>
{
    public static DishQuantities From(DataTable table)
    {
        var lines = new DishQuantities();
        lines.AddRange(table.Rows.Select(row => (row["dish"], int.Parse(row["quantity"], CultureInfo.InvariantCulture))));
        return lines;
    }

    public IReadOnlyDictionary<string, int> ByDish() => this.ToDictionary(line => line.Dish, line => line.Quantity);
}

/// <summary>A `| dish | price |` table: dishes the restaurant serves for the scenario.</summary>
public sealed class DishPrices : List<(string Label, decimal Price)>
{
    public static DishPrices From(DataTable table)
    {
        var dishes = new DishPrices();
        dishes.AddRange(table.Rows.Select(row => (row["dish"], decimal.Parse(row["price"], CultureInfo.InvariantCulture))));
        return dishes;
    }
}
