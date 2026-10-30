using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

/// <summary>The owner books one of the waiter's location's own tables for <paramref name="seats"/> people (themselves
/// included), which opens a group pre-order; the group is found the way a guest finds it, through the invite link.
/// The waiter's location means the reservation is always assigned to that waiter.
///
/// Tables are the restaurant's floor plan, not test data: they are never created or removed here. Removing a table
/// a cancelled reservation still points to breaks the waiter's whole reservation list (the backend fails it on the
/// first reservation whose table is missing). Instead the booking goes to a random free hour 3–60 days ahead — well
/// clear of the 30-minute pre-order cut-off — and another hour is tried if a parallel scenario took that table first.
/// The reservation is cancelled afterwards by the waiter, which also works once the group is submitted.</summary>
public sealed class BookAGroupTable(int seats, string name = GroupBooking.TheGroup) : ITask
{
    private const int Attempts = 5;

    public async Task PerformAsAsync(Actor actor)
    {
        var owner = actor.Recall<CustomerAccount>(CustomerAccount.Owner);
        var waiter = await actor.AsksForAsync(new TheWaiterSession());
        var location = await actor.AsksForAsync(new TheWaitersLocation());
        var response = actor.TheLastResponse();
        var tables = new TablesApi(actor.UsingTheApi());
        var reservations = new ReservationsApi(actor.UsingTheApi());

        for (var attempt = 1; ; attempt++)
        {
            var start = DateTime.UtcNow.Date.AddDays(Random.Shared.Next(3, 61)).AddHours(Random.Shared.Next(10, 21));
            var free = await response.CaptureRequiredAsync<List<TableResponse>>(
                await tables.GetAvailableTablesAsync(location.LocationId, seats, start), "Looking for a free table");
            var table = free.Where(t => t.Capacity >= seats).OrderBy(_ => Random.Shared.Next()).FirstOrDefault();

            if (table is not null)
            {
                await response.CaptureAsync(await reservations.CreateReservationAsync(
                    SampleDataFactory.CreateReservationRequest(table.Id, start, seats), owner.IdToken));
                if (response.Succeeded)
                {
                    var reservation = response.Json<ReservationResponse>();
                    actor.Recall<CleanupScope>().TrackDelete(
                        $"reservation:{reservation.Id}", () => reservations.CancelReservationAsync(reservation.Id, waiter.IdToken));
                    await RememberTheGroupAsync(actor, owner, reservation, table);
                    return;
                }
            }

            if (attempt == Attempts)
                throw new InvalidOperationException($"No table for {seats} could be booked in {Attempts} tries; last answer {response.Status}: {response.Body}");
        }
    }

    private async Task RememberTheGroupAsync(Actor actor, CustomerAccount owner, ReservationResponse reservation, TableResponse table)
    {
        var response = actor.TheLastResponse();
        var link = await response.CaptureRequiredAsync<InviteTokenResponse>(
            await new ReservationsApi(actor.UsingTheApi()).GetInviteTokenAsync(reservation.Id, owner.IdToken), "Getting the invite link");
        var invite = await response.CaptureRequiredAsync<InviteResponse>(
            await new GuestSessionsApi(actor.UsingTheApi()).GetInviteAsync(link.Token), "Opening the invite link");

        actor.Remember(name, new GroupBooking(reservation.Id, invite.GroupReservationId, table.Id, table.TableNumber, seats));
    }
}
