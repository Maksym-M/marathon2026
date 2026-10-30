namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>One participant of a group: <see cref="Type"/> is "Owner" or "Guest"; the Owner is always submitted.</summary>
public sealed record ParticipantResponse(
    string Id,
    string Type,
    string Name,
    IReadOnlyList<ParticipantItemResponse> Items,
    decimal Total,
    bool IsSubmitted);
