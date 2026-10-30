using Restaurant_Aqa_Framework.Api.Dtos.Orders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>The pre-order of the group's reservation as the restaurant receives it: its status and dishes by label.</summary>
public sealed record PreOrderView(OrderStatus Status, IReadOnlyDictionary<string, int> Dishes);
