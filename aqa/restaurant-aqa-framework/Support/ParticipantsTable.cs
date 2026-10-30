using System.Globalization;
using Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

namespace Restaurant_Aqa_Framework.Support;

/// <summary>The participants a feature expects, as a `| participant | dishes | total | status |` table. Only the
/// columns the table has are compared, and a cell of "-" is not compared, so a UI scenario can leave out what the page
/// does not show; every listed participant must be there, and nobody else.</summary>
public static class ParticipantsTable
{
    private const string NotCompared = "-";

    public static IReadOnlyList<string> Mismatches(DataTable expected, IReadOnlyList<ParticipantView> actual)
    {
        var problems = new List<string>();
        foreach (var row in expected.Rows)
        {
            var name = row["participant"];
            var participant = actual.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (participant is null)
            {
                problems.Add($"{name}: not listed");
                continue;
            }

            if (row.TryGetValue("dishes", out var dishes) && dishes != NotCompared && !participant.Dishes.SameAs(DishList.Parse(dishes)))
                problems.Add($"{name}: expected dishes \"{dishes}\" but were \"{DishList.Format(participant.Dishes)}\"");

            if (row.TryGetValue("total", out var total) && total != NotCompared && participant.Total != decimal.Parse(total, CultureInfo.InvariantCulture))
                problems.Add($"{name}: expected a total of {total} but it was {participant.Total?.ToString(CultureInfo.InvariantCulture) ?? "not shown"}");

            if (row.TryGetValue("status", out var status) && status != NotCompared && (status == "submitted") != participant.Submitted)
                problems.Add($"{name}: expected \"{status}\" but was \"{(participant.Submitted ? "submitted" : "not submitted")}\"");
        }

        var expectedNames = expected.Rows.Select(r => r["participant"]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        problems.AddRange(actual.Where(p => !expectedNames.Contains(p.Name)).Select(p => $"{p.Name}: listed but not expected"));
        return problems;
    }

    public static string Describe(IReadOnlyList<ParticipantView> participants) =>
        string.Join("; ", participants.Select(p =>
            $"{p.Name}: {DishList.Format(p.Dishes)}, total {p.Total?.ToString(CultureInfo.InvariantCulture) ?? "-"}, {(p.Submitted ? "submitted" : "not submitted")}"));
}
