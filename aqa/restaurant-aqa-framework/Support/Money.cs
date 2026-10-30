using System.Globalization;
using System.Text.RegularExpressions;

namespace Restaurant_Aqa_Framework.Support;

/// <summary>Reads an amount the storefront shows ("$1,027.50") as a number.</summary>
public static partial class Money
{
    public static decimal Parse(string shown) =>
        decimal.Parse(NotPartOfTheNumber().Replace(shown, ""), NumberStyles.Number, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"[^\d.\-]")]
    private static partial Regex NotPartOfTheNumber();
}
