namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>The dish of a participant's line. The response carries the full dish; only what the suite compares is modelled.</summary>
public sealed record ParticipantDishResponse(string Id, string Name, decimal Price);
