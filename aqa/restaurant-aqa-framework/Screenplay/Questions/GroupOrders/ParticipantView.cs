namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>One participant as a person sees them, whether read from the service or from the page: the owner is
/// called <see cref="CustomerAccount.Owner"/>, guests by their name; dishes by the label the feature gave them.
/// <see cref="Total"/> is null where the page shows no subtotal (a cart that is not submitted).</summary>
public sealed record ParticipantView(string Name, IReadOnlyDictionary<string, int> Dishes, decimal? Total, bool Submitted);
