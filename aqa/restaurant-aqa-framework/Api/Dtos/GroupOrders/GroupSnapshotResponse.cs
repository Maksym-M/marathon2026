namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>GET /guest/groups/{groupReservationId}/sessions (and the body of a participant removal). The Owner is
/// always first; <see cref="Total"/> adds up every cart, submitted or not.</summary>
public sealed record GroupSnapshotResponse(IReadOnlyList<ParticipantResponse> Participants, decimal Total);
