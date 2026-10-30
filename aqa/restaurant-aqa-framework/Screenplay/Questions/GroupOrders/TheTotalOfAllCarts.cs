using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>The total the last answer of the group service gave — every cart, submitted or not.</summary>
public sealed class TheTotalOfAllCarts : IQuestion<decimal>
{
    public Task<decimal> AnsweredByAsync(Actor actor) => Task.FromResult(actor.TheLastResponse().Json<GroupSnapshotResponse>().Total);
}
