using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>A guest follows the invite link of a group and joins under their name; remembered under that name.</summary>
public sealed class JoinTheGroup(string guest, string group = GroupBooking.TheGroup) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var booking = actor.Recall<GroupBooking>(group);
        var session = await actor.TheLastResponse().CaptureRequiredAsync<JoinGroupResponse>(
            await new GuestSessionsApi(actor.UsingTheApi()).JoinAsync(booking.GroupReservationId, guest), $"{guest} joining {group}");

        actor.Remember(guest, new GuestSession(guest, session.SessionId, session.SessionToken, booking.GroupReservationId));
    }
}
