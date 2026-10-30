namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>POST /guest/sessions answers <c>{"SessionId", "SessionToken"}</c> (PascalCase). The token is the guest's
/// bearer for every /guest/sessions/{id}/… call.</summary>
public sealed record JoinGroupResponse(string SessionId, string SessionToken);
