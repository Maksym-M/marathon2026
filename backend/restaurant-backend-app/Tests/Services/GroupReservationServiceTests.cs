using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using AutoFixture.Xunit2;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupReservationServiceTests
{
    private readonly Mock<IGroupReservationRepository> _groupReservationRepositoryMock = new();
    private readonly Mock<ITableRepository> _tableRepositoryMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IGuestSessionRepository> _guestSessionRepositoryMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();

    private readonly GroupReservationService _service;

    public GroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groupReservationRepositoryMock.Object,
            _tableRepositoryMock.Object,
            _reservationRepositoryMock.Object,
            _guestSessionRepositoryMock.Object,
            _orderRepositoryMock.Object,
            _userServiceMock.Object,
            _locationServiceMock.Object);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_ValidInput_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Table table,
        Reservation reservation,
        Location location,
        string expectedUserName)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.TableId = table.Id;
        table.LocationId = location.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(0);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        _locationServiceMock
            .Setup(s => s.GetLocationByIdAsync(table.LocationId))
            .ReturnsAsync(location);

        _userServiceMock
            .Setup(s => s.GetUserName(reservation.CustomerId!))
            .ReturnsAsync(expectedUserName);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
        Assert.Equal(table.LocationId, result.LocationId);
        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        Assert.Equal(reservation.StartTime, result.StartTime);
        Assert.Equal(reservation.EndTime, result.EndTime);
        Assert.Equal(table.TableNumber, result.TableNumber);
        Assert.Equal("Green & Tasty", result.RestaurantName);
        Assert.Equal(location.Address, result.LocationAddress);
        Assert.Equal(expectedUserName, result.InviterName);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_InvalidToken_ThrowsNotFound(string token)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(token))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupReservationByTokenAsync(token));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed)]
    [InlineAutoData(GroupReservationStatus.Submitted)]
    public async Task GetGroupReservationByTokenAsync_GroupReservationIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        GroupReservation groupReservation)
    {
        groupReservation.Status = status;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_WhenGuestCountIsMax_ThrowsGuestLimitExceeded(GroupReservation groupReservation, Reservation reservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = reservation.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(reservation.GuestCount);

        await Assert.ThrowsAsync<GuestLimitExceededException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_ValidInput_UpdatesGroupReservationStatus(GroupReservation groupReservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(groupReservation);

        GroupReservation? savedGroupReservation = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(r => savedGroupReservation = r);

        await _service.CloseGroupReservationAsync(groupReservation.ReservationId);

        Assert.NotNull(savedGroupReservation);
        Assert.Equal(GroupReservationStatus.Closed, savedGroupReservation.Status);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_GroupReservationNotFound_DoesNotThrowException(string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CloseGroupReservationAsync(reservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_ValidInput_ReturnsToken(Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        GetGroupInviteTokenResponse result = await _service.GetGroupReservationInviteTokenAsync(reservation.Id);

        Assert.Equal(groupReservation.ReservationToken, result.Token);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_InvalidReservationId_ThrowsNotFound(string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetGroupReservationInviteTokenAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_OneGuest_ThrowsValidationException(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Completed)]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    public async Task GetGroupReservationInviteTokenAsync_StatusIsNotReserved_ThrowsValidationException(ReservationStatus reservationStatus, Reservation reservation)
    {
        reservation.Status = reservationStatus;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    public async Task GetGroupReservationInviteTokenAsync_GroupStatusIsNotOpen_ThrowsException(GroupReservationStatus status, Type type, Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = status;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(type, () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_ValidInput_MergesSubmittedDishesIntoOrder(
        GroupReservation groupReservation,
        Reservation reservation,
        Order order,
        string userId)
    {
        SetupSubmittableGroup(groupReservation, reservation, userId);
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([
                CreateSession(groupReservation.GroupReservationId, isSubmitted: true, ("dish1", 2), ("dish2", 1)),
                CreateSession(groupReservation.GroupReservationId, isSubmitted: true, ("dish1", 1)),
                CreateSession(groupReservation.GroupReservationId, isSubmitted: false, ("dish3", 5)),
            ]);

        order.Status = OrderStatus.Created;
        order.Dishes = [new OrderItem { DishId = "dish2", Quantity = 1, State = OrderItemState.Confirmed }];
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync(order);

        var result = await _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id);

        Assert.Equal([new("dish1", 3), new("dish2", 1)], result.OrderBy(i => i.DishId));

        var dish1 = Assert.Single(order.Dishes, d => d.DishId == "dish1");
        Assert.Equal((3, OrderItemState.Pending), (dish1.Quantity, dish1.State));
        var dish2 = Assert.Single(order.Dishes, d => d.DishId == "dish2");
        Assert.Equal((2, OrderItemState.Pending), (dish2.Quantity, dish2.State));
        Assert.DoesNotContain(order.Dishes, d => d.DishId == "dish3");

        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(GroupReservationStatus.Submitted, groupReservation.Status);
        _groupReservationRepositoryMock.Verify(r => r.SaveWithOrderAsync(groupReservation, order), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_GroupNotFound_ThrowsGroupReservationNotFoundException(string userId, string groupReservationId, string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId));
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_ReservationNotFound_ThrowsReservationNotFoundException(
        GroupReservation groupReservation,
        string userId,
        string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservationId));
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_NotOwner_ThrowsReservationNotFoundException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string otherUserId)
    {
        SetupSubmittableGroup(groupReservation, reservation, userId);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.SubmitGroupPreOrderAsync(otherUserId, groupReservation.GroupReservationId, reservation.Id));
        VerifyNothingSaved();
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_GroupOfAnotherReservation_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string otherReservationId)
    {
        SetupSubmittableGroup(groupReservation, reservation, userId);
        groupReservation.ReservationId = otherReservationId;

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));
        VerifyNothingSaved();
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task SubmitGroupPreOrderAsync_GroupNotOpen_Throws(
        GroupReservationStatus status,
        Type expectedException,
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupSubmittableGroup(groupReservation, reservation, userId);
        groupReservation.Status = status;

        await Assert.ThrowsAsync(expectedException, () => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));
        VerifyNothingSaved();
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    [InlineAutoData(ReservationStatus.Completed)]
    public async Task SubmitGroupPreOrderAsync_ReservationNotReserved_ThrowsValidationException(
        ReservationStatus status,
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupSubmittableGroup(groupReservation, reservation, userId);
        reservation.Status = status;

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));
        VerifyNothingSaved();
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_AfterCutoff_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupSubmittableGroup(groupReservation, reservation, userId);
        reservation.StartTime = DateTime.UtcNow.AddMinutes(20);

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));
        VerifyNothingSaved();
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_OnlyOwnerHasDishes_SubmitsOwnerOrder(
        GroupReservation groupReservation,
        Reservation reservation,
        Order order,
        string userId)
    {
        SetupSubmittableGroup(groupReservation, reservation, userId);
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([CreateSession(groupReservation.GroupReservationId, isSubmitted: false, ("dish1", 1))]);
        order.Status = OrderStatus.Created;
        order.Dishes = [new OrderItem { DishId = "dish2", Quantity = 2, State = OrderItemState.Pending }];
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync(order);

        var result = await _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id);

        Assert.Empty(result);
        Assert.Equal((2, "dish2"), (Assert.Single(order.Dishes).Quantity, order.Dishes[0].DishId));
        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(GroupReservationStatus.Submitted, groupReservation.Status);
        _groupReservationRepositoryMock.Verify(r => r.SaveWithOrderAsync(groupReservation, order), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_NothingToSubmit_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        Order order,
        string userId)
    {
        SetupSubmittableGroup(groupReservation, reservation, userId);
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([
                CreateSession(groupReservation.GroupReservationId, isSubmitted: true),
                CreateSession(groupReservation.GroupReservationId, isSubmitted: false, ("dish1", 1)),
            ]);
        // Only a line the restaurant cancelled — nothing that would actually be served.
        order.Dishes = [new OrderItem { DishId = "dish2", Quantity = 1, State = OrderItemState.Canceled }];
        _orderRepositoryMock.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync(order);

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));
        VerifyNothingSaved();
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_OrderNotFound_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupSubmittableGroup(groupReservation, reservation, userId);
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([CreateSession(groupReservation.GroupReservationId, isSubmitted: true, ("dish1", 1))]);
        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));
        VerifyNothingSaved();
    }

    private void SetupSubmittableGroup(GroupReservation groupReservation, Reservation reservation, string userId)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.CustomerId = userId;
        reservation.Status = ReservationStatus.Reserved;
        reservation.StartTime = DateTime.UtcNow.AddDays(1);

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
    }

    private void VerifyNothingSaved() =>
        _groupReservationRepositoryMock.Verify(r => r.SaveWithOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>()), Times.Never);

    private static GuestSession CreateSession(string groupReservationId, bool isSubmitted, params (string DishId, int Quantity)[] dishes) => new()
    {
        Id = Guid.NewGuid().ToString(),
        GroupReservationId = groupReservationId,
        GuestName = "Guest",
        IsSubmitted = isSubmitted,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity }).ToList(),
    };
}
