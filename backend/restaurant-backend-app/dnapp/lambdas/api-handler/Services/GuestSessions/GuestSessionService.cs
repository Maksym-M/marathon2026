using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Constants;
using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Mappers;
using APIHandler.Models.GuestSessions;
using APIHandler.Models.Orders;
using APIHandler.Models.Reservations;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using APIHandler.Services.Notifications;

namespace APIHandler.Services.GuestSessions;

public class GuestSessionService(
    IGroupReservationRepository groupReservationRepository,
    IGuestSessionRepository guestSessionRepository,
    IDishRepository dishRepository,
    IReservationRepository reservationRepository,
    IUserRepository userRepository,
    ITableRepository tableRepository,
    IOrderRepository orderRepository) : IGuestSessionService
{
    private const string OwnerDefaultName = "Owner";

    public async Task<bool> IsAuthorizedAsync(string guestSessionToken, string guestSessionId)
    {
        string hash = Convert.ToBase64String(SHA256.HashData(Convert.FromBase64String(guestSessionToken)));
        GuestSession? guestSession = await guestSessionRepository.GetByTokenHash(hash);
        if (guestSession is null)
            return false;

        return guestSession.Id == guestSessionId;
    }

    public async Task<CreatGuestSessionResponse> CreateGuestSessionAsync(CreateGuestSessionRequest request)
    {
        GroupReservation group = await GetGroupReservationAndValidateAsync(request.GroupReservationId);

        Task<Reservation?> reservationTask = reservationRepository.GetReservationByIdAsync(group.ReservationId);
        Task<int> countTask = guestSessionRepository.CountByGroupReservationId(group.GroupReservationId);

        Reservation reservation = await reservationTask ?? throw new GroupReservationExpiredException($"Associated reservation for group '{request.GroupReservationId}' not found.");

        if (reservation.Status != ReservationStatus.Reserved)
        {
            throw new GuestSessionExpiredException($"This pre-order session has expired or is no longer active. Current reservation status: '{reservation.Status}'.");
        }

        int joinedGuestsCount = await countTask;

        if (joinedGuestsCount + 1 >= reservation.GuestCount)
            throw new GuestLimitExceededException($"Guest limit exceeded. Maximum capacity is {reservation.GuestCount}, but {joinedGuestsCount} guests have already joined.");

        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);

        var session = new GuestSession()
        {
            Id = Guid.NewGuid().ToString(),
            GuestName = request.GuestName,
            GroupReservationId = group.GroupReservationId,
            SessionTokenHash = Convert.ToBase64String(SHA256.HashData(tokenBytes)),
            Dishes = [],
        };

        await guestSessionRepository.SaveAsync(session);

        return new CreatGuestSessionResponse(session.Id, Convert.ToBase64String(tokenBytes));
    }

    public async Task<GuestMenuResponse> GetGuestMenuAsync(string guestSessionId)
    {
        var session = await guestSessionRepository.GetByGuestSessionId(guestSessionId)
            ?? throw new ValidationException($"Guest session '{guestSessionId}' not found.");

        await ValidateGroupReservationId(session.GroupReservationId);

        string locationId = await GetLocationIdForGroupAsync(session.GroupReservationId);
        var dishes = await dishRepository.GetDishesByLocationIdAsync(locationId);

        var dishesMapped = dishes
            .GroupBy(d => d.DishType)
            .ToDictionary(k => k.Key, GuestMenuDishMapper.Map);

        return new GuestMenuResponse(dishesMapped);
    }

    public async Task AddDishAsync(string guestSessionId, string dishId, int quantity)
    {
        if (quantity <= 0)
            throw new ValidationException($"Quantity ({quantity}) must be greater than zero.");

        GuestSession session = await GetGuestSessionAsync(guestSessionId);

        string locationId = await GetLocationIdForGroupAsync(session.GroupReservationId);
        var locationDishes = await dishRepository.GetDishesByLocationIdAsync(locationId);

        var dish = locationDishes.FirstOrDefault(d => d.Id == dishId)
            ?? throw new ValidationException($"Dish '{dishId}' does not exist or is not available at this location.");

        if (dish.State != DishState.Available)
            throw new DishIsNotAvailableException($"Dish '{dishId}' is currently unavailable. State: '{dish.State}'.");

        session.Dishes ??= [];

        var existingDish = session.Dishes.FirstOrDefault(d => d.DishId == dishId);

        if (existingDish != null)
        {
            existingDish.Quantity += quantity;
        }
        else
        {
            session.Dishes.Add(new OrderItem
            {
                DishId = dishId,
                Quantity = quantity,
                State = OrderItemState.Pending
            });
        }

        await guestSessionRepository.SaveAsync(session);
    }

    public async Task RemoveDishAsync(string guestSessionId, string dishId, int quantity)
    {
        if (quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");

        GuestSession session = await GetGuestSessionAsync(guestSessionId);

        if (session.Dishes == null || session.Dishes.Count == 0)
            throw new DishNotFoundException($"Dish {dishId} not found in guest session.");

        var existingDish = session.Dishes.FirstOrDefault(d => d.DishId == dishId)
            ?? throw new DishNotFoundException($"Dish {dishId} not found in guest session.");

        existingDish.Quantity -= quantity;

        if (existingDish.Quantity <= 0)
        {
            session.Dishes.Remove(existingDish);
        }

        await guestSessionRepository.SaveAsync(session);
    }

    public async Task<GroupReservationStatusResponse> GetGroupReservationStatusAsync(string groupReservationId)
    {
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();

        var reservation = await reservationRepository.GetReservationByIdAsync(group.ReservationId)
            ?? throw new InvalidOperationException("Associated reservation not found.");

        int currentParticipants = await GetCurrentParticipantCountAsync(groupReservationId);

        return new GroupReservationStatusResponse
        {
            CurrentParticipants = currentParticipants,
            MaxCapacity = reservation.GuestCount,
            Status = group.Status
        };
    }

    public async Task<GetAllSessionsResponse> GetGroupSessionsAsync(string userId, string groupReservationId)
    {
        var (group, reservation) = await GetOwnedGroupReservationAsync(groupReservationId, userId);

        return await BuildGroupSnapshotAsync(group, reservation);
    }

    public async Task<GetAllSessionsResponse> DeleteGuestSessionAsync(string userId, string groupReservationId, string guestSessionId)
    {
        var (group, reservation) = await GetOwnedGroupReservationAsync(groupReservationId, userId);

        var session = await guestSessionRepository.GetByGuestSessionId(guestSessionId)
            ?? throw new GuestSessionNotFoundException($"Guest session with id {guestSessionId} not found.");

        if (session.GroupReservationId != groupReservationId)
        {
            throw new ValidationException("Guest session does not belong to this group reservation.");
        }

        if (session.IsOwnerSession)
        {
            throw new ValidationException("The owner's session cannot be removed from the group.");
        }

        ValidateGroupReservation(group);

        await guestSessionRepository.DeleteAsync(session);

        return await BuildGroupSnapshotAsync(group, reservation, removedSessionId: session.Id);
    }

    public async Task<GetAllSessionsResponse> UpdateSessionDishQuantityAsync(string userId, string groupReservationId, string guestSessionId, string dishId, int quantity)
    {
        if (quantity < 0)
            throw new ValidationException($"Quantity ({quantity}) must be zero or greater.");

        var (group, reservation) = await GetOwnedGroupReservationAsync(groupReservationId, userId);

        // The Owner's own dishes live in the reservation's Order (added from the menu), not in a guest session.
        if (guestSessionId == GetOwnerSessionId(groupReservationId))
        {
            ValidateGroupReservation(group);
            PreOrderHelper.EnsurePreOrderEditable(reservation.StartTime);
            await UpdateOwnerOrderDishAsync(reservation.Id, dishId, quantity);
            return await BuildGroupSnapshotAsync(group, reservation);
        }

        var session = await guestSessionRepository.GetByGuestSessionId(guestSessionId)
            ?? throw new GuestSessionNotFoundException($"Guest session with id {guestSessionId} not found.");

        if (session.GroupReservationId != groupReservationId)
            throw new ValidationException("Guest session does not belong to this group reservation.");

        // A cart that is still being chosen belongs to the guest; the Owner edits only what was handed over.
        if (!session.IsOwnerSession && !session.IsSubmitted)
            throw new ValidationException("Only submitted guest pre-orders can be edited.");

        ValidateGroupReservation(group);
        PreOrderHelper.EnsurePreOrderEditable(reservation.StartTime);

        session.Dishes ??= [];
        var item = session.Dishes.FirstOrDefault(d => d.DishId == dishId)
            ?? throw new DishNotFoundException($"Dish {dishId} not found in guest session.");

        if (quantity > item.Quantity)
            await ValidateDishesAsync([item]);

        if (quantity == 0)
            session.Dishes.Remove(item);
        else
            item.Quantity = quantity;

        // An empty submitted cart counts for nothing, so the guest goes back to "not submitted".
        if (!session.IsOwnerSession && session.Dishes.Count == 0)
            session.IsSubmitted = false;

        await guestSessionRepository.SaveAsync(session);

        return await BuildGroupSnapshotAsync(group, reservation);
    }

    private async Task UpdateOwnerOrderDishAsync(string reservationId, string dishId, int quantity)
    {
        var order = await GetActiveOrderAsync(reservationId)
            ?? throw new DishNotFoundException($"Dish {dishId} not found in the owner's pre-order.");
        var item = order.Dishes.FirstOrDefault(d => d.DishId == dishId && d.State != OrderItemState.Canceled)
            ?? throw new DishNotFoundException($"Dish {dishId} not found in the owner's pre-order.");

        if (quantity > item.Quantity)
            await ValidateDishesAsync([item]);

        if (quantity == 0)
        {
            order.Dishes.Remove(item);
        }
        else
        {
            item.Quantity = quantity;
            // The quantity changed, so the restaurant has to review this line again.
            item.State = OrderItemState.Pending;
        }

        await orderRepository.SaveOrderAsync(order);
    }

    public async Task<GroupReservationStatusResponse> RemoveEmptySeatAsync(string userId, string groupReservationId)
    {
        var (group, reservation) = await GetOwnedGroupReservationAsync(groupReservationId, userId);

        ValidateGroupReservation(group);
        PreOrderHelper.EnsurePreOrderEditable(reservation.StartTime);

        // Only a seat nobody has taken can go: the table never gets fewer seats than people who joined.
        int participants = await GetCurrentParticipantCountAsync(groupReservationId);
        if (reservation.GuestCount <= participants)
            throw new ValidationException("There is no empty seat to remove: every seat has a participant.");

        reservation.GuestCount--;
        await reservationRepository.SaveReservationAsync(reservation);

        return new GroupReservationStatusResponse
        {
            CurrentParticipants = participants,
            MaxCapacity = reservation.GuestCount,
            Status = group.Status
        };
    }

    public async Task<GroupPreOrderSummaryResponse?> GetGroupPreOrderSummaryAsync(Reservation reservation, Order? order)
    {
        if (reservation.GuestCount < 2)
            return null;

        var group = await groupReservationRepository.GetByReservationIdAsync(reservation.Id);
        if (group is null)
            return null;

        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(group.GroupReservationId);
        var guests = sessions.Where(s => !s.IsOwnerSession).ToList();
        var activeOrder = order is null || order.Status == OrderStatus.Cancelled ? null : order;
        bool ownerHasDishes = GetOwnerDishes(group, activeOrder, sessions).Count > 0;
        int joined = guests.Count + 1;

        return new GroupPreOrderSummaryResponse
        {
            Status = group.Status,
            Submitted = (ownerHasDishes ? 1 : 0) + guests.Count(g => g.IsSubmitted && g.Dishes.Count > 0),
            Joined = joined,
            Seats = Math.Max(reservation.GuestCount, joined),
        };
    }

    public async Task<int?> GetParticipantCountByReservationIdAsync(string reservationId)
    {
        var group = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        return group is null ? null : await GetCurrentParticipantCountAsync(group.GroupReservationId);
    }

    public async Task<string> GetOwnerSessionIdAsync(string userId, string reservationId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId);

        // Non-owners get the same 404 as a missing reservation, so they can't find out which reservations exist.
        if (reservation is null || reservation.CustomerId != userId)
            throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");

        var group = await groupReservationRepository.GetByReservationIdAsync(reservationId)
            ?? throw new GroupReservationNotFoundException();

        GuestSession session = await GetOrCreateOwnerSessionAsync(userId, group.GroupReservationId);
        return session.Id;
    }

    public async Task SubmitCartAsync(string guestSessionId)
    {
        GuestSession guestSession = await GetGuestSessionAsync(guestSessionId);

        if (!guestSession.Dishes.Any())
            throw new CartIsEmptyException();

        await ValidateDishesAsync(guestSession.Dishes);

        guestSession.IsSubmitted = true;
        await guestSessionRepository.SaveAsync(guestSession);
    }

    private async Task<GroupReservation> GetGroupReservationAndValidateAsync(string groupReservationId)
    {
        GroupReservation? group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId);
        ValidateGroupReservation(group);
        return group;
    }

    private async Task<int> GetCurrentParticipantCountAsync(string groupReservationId)
    {
        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId);

        // Owner is always 1 active participant, whether or not their own session exists yet.
        // If sessions is null/empty, it safely evaluates to 0 joined guests + 1 owner = 1.
        return (sessions?.Count(s => !s.IsOwnerSession) ?? 0) + 1;
    }

    private async Task ValidateGroupReservationId(string groupReservationId) => await GetGroupReservationAndValidateAsync(groupReservationId);

    private static void ValidateGroupReservation([NotNull] GroupReservation? group)
    {
        if (group is null)
            throw new GroupReservationNotFoundException();

        if (group.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        if (group.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();
    }

    private async Task<GuestSession> GetGuestSessionAsync(string sessionId)
    {
        var session = await guestSessionRepository.GetByGuestSessionId(sessionId)
            ?? throw new GuestSessionNotFoundException($"Guest session {sessionId} not found.");

        if (session.IsSubmitted)
            throw new GuestSessionSubmittedException();

        await ValidateGroupReservationId(session.GroupReservationId);

        return session;
    }

    private async Task ValidateDishesAsync(IEnumerable<OrderItem> orders)
    {
        IEnumerable<string> dishIds = orders.Select(o => o.DishId).ToArray();
        IEnumerable<Dish> dishes = await dishRepository.GetDishesByIdsAsync(dishIds);

        if (dishes.FirstOrDefault(d => d.State != DishState.Available) is Dish dish)
            throw new DishIsNotAvailableException($"The dish '{dish.Name}' ({dish.Id}) is not available.");
    }

    private async Task<string> GetLocationIdForGroupAsync(string groupReservationId)
    {
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();

        var reservation = await reservationRepository.GetReservationByIdAsync(group.ReservationId)
            ?? throw new ReservationNotFoundException("Parent reservation not found.");

        var table = await tableRepository.GetTableAsync(reservation.TableId)
            ?? throw new ValidationException("Associated table not found.");

        return table.LocationId;
    }

    private async Task<(GroupReservation Group, Reservation Reservation)> GetOwnedGroupReservationAsync(string groupReservationId, string userId)
    {
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();

        var reservation = await reservationRepository.GetReservationByIdAsync(group.ReservationId);

        // Non-owners get the same 404 as a missing group, so they can't find out which groups exist.
        if (reservation is null || reservation.CustomerId != userId)
            throw new GroupReservationNotFoundException();

        return (group, reservation);
    }

    // The id is derived from the group, so concurrent calls resolve to the same row instead of creating duplicates.
    private static string GetOwnerSessionId(string groupReservationId) => $"owner-{groupReservationId}";

    private async Task<GuestSession> GetOrCreateOwnerSessionAsync(string userId, string groupReservationId)
    {
        string ownerSessionId = GetOwnerSessionId(groupReservationId);

        var existing = await guestSessionRepository.GetByGuestSessionId(ownerSessionId);
        if (existing is not null)
            return existing;

        var session = new GuestSession()
        {
            Id = ownerSessionId,
            GroupReservationId = groupReservationId,
            GuestName = OwnerDefaultName,
            Dishes = [],
            IsOwnerSession = true,
            OwnerUserId = userId,
        };

        await guestSessionRepository.SaveAsync(session);
        return session;
    }

    /// <param name="removedSessionId">A session deleted just before: never listed, whatever the read still returns.</param>
    private async Task<GetAllSessionsResponse> BuildGroupSnapshotAsync(GroupReservation group, Reservation reservation, string? removedSessionId = null)
    {
        var sessions = (await guestSessionRepository.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .Where(s => s.Id != removedSessionId)
            .ToList();
        var ownerDishes = GetOwnerDishes(group, await GetActiveOrderAsync(reservation.Id), sessions);

        var dishIds = sessions
            .SelectMany(s => s.Dishes)
            .Concat(ownerDishes)
            .Select(s => s.DishId)
            .Distinct()
            .ToArray();

        var allDishes = await dishRepository.GetDishesByIdsAsync(dishIds);
        var dishesById = allDishes.ToDictionary(d => d.Id);

        // The owner is always a participant and always first. Their dishes are always counted: the Owner is the one who sends the order.
        string ownerName = await GetOwnerNameAsync(reservation.CustomerId!);
        var ownerItems = MapItems(ownerDishes, dishesById);
        var participants = new List<Participant>
        {
            new(GetOwnerSessionId(group.GroupReservationId), ParticipantType.Owner, ownerName, ownerItems,
                ownerItems.Sum(i => i.Quantity * i.Dish.Price), true)
        };

        participants.AddRange(sessions
            .Where(s => !s.IsOwnerSession)
            .OrderByDescending(s => s.IsSubmitted)
            .ThenBy(s => s.GuestName, StringComparer.OrdinalIgnoreCase)
            .Select(s => ToParticipant(s, ParticipantType.Guest, s.GuestName, dishesById)));

        return new GetAllSessionsResponse(participants, participants.Sum(p => p.Total));
    }

    private async Task<Order?> GetActiveOrderAsync(string reservationId)
    {
        var order = await orderRepository.GetOrderByReservationIdAsync(reservationId);
        return order is null || order.Status == OrderStatus.Cancelled ? null : order;
    }

    /// <summary>
    /// The Owner's dishes from the reservation's Order. After the group is submitted the Order also holds the guests'
    /// submitted dishes (merged by dishId), so those are subtracted again to show only what the Owner ordered.
    /// </summary>
    private static List<OrderItem> GetOwnerDishes(GroupReservation group, Order? order, IEnumerable<GuestSession> sessions)
    {
        var dishes = (order?.Dishes ?? [])
            .Where(d => d.State != OrderItemState.Canceled)
            .Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity, State = d.State })
            .ToList();

        if (group.Status != GroupReservationStatus.Submitted)
            return dishes;

        var guestQuantities = sessions
            .Where(s => !s.IsOwnerSession && s.IsSubmitted)
            .SelectMany(s => s.Dishes)
            .GroupBy(d => d.DishId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));

        foreach (var dish in dishes)
            dish.Quantity -= guestQuantities.GetValueOrDefault(dish.DishId);

        return dishes.Where(d => d.Quantity > 0).ToList();
    }

    private static Participant ToParticipant(GuestSession session, ParticipantType type, string name, IReadOnlyDictionary<string, Dish> dishesById)
    {
        var items = MapItems(session.Dishes, dishesById);
        decimal total = items.Sum(i => i.Quantity * i.Dish.Price);

        return new Participant(session.Id, type, name, items, total, session.IsSubmitted);
    }

    // Dishes removed from the menu can't be priced, so they are left out instead of failing the whole response.
    private static OrderItemResponse[] MapItems(IEnumerable<OrderItem> dishes, IReadOnlyDictionary<string, Dish> dishesById) =>
        dishes
            .Where(d => dishesById.ContainsKey(d.DishId))
            .Select(d => new OrderItemResponse
            {
                Dish = DishMapper.ToDishResponse(dishesById[d.DishId]),
                Quantity = d.Quantity
            })
            .ToArray();

    private async Task<string> GetOwnerNameAsync(string customerId)
    {
        var user = await userRepository.GetUserByIdAsync(customerId);
        return user?.Attributes?.FirstOrDefault(a => a.Name == "given_name")?.Value ?? OwnerDefaultName;
    }

    public async Task<GetGuestSessionResponse> GetGuestSessionInfoAsync(string guestSessionId)
    {
        var session = await guestSessionRepository.GetByGuestSessionId(guestSessionId)
            ?? throw new GuestSessionNotFoundException("Guest session was not found.");

        var dishIds = session.Dishes
            .Select(s => s.DishId)
            .Distinct()
            .ToArray();

        var allDishes = await dishRepository.GetDishesByIdsAsync(dishIds);
        var dishesById = allDishes.ToDictionary(d => d.Id);

        var items = MapItems(session.Dishes, dishesById);

        decimal total = items.Sum(i => i.Quantity * i.Dish.Price);

        string name = session.GuestName;
        if (session.IsOwnerSession)
        {
            var groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(session.GroupReservationId);
            var reservation = await reservationRepository.GetReservationByIdAsync(groupReservation!.ReservationId);
            name = await GetOwnerNameAsync(reservation!.CustomerId!);
        }

        return new GetGuestSessionResponse(name, items, total, session.IsSubmitted);
    }

}




