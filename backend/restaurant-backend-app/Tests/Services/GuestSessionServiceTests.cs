using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using AutoFixture.Xunit2;
using Moq;
using NPOI.POIFS.Storage;
using System.Security.Cryptography;
using System.Text;

namespace APIHandler.Tests.Services;

public class GuestSessionServiceTests
{
    private readonly Mock<IGuestSessionRepository> _mockGuestSessionRepository = new();
    private readonly Mock<IGroupReservationRepository> _mockGroupReservationRepository = new();
    private readonly Mock<IDishRepository> _mockDishRepository = new();
    private readonly Mock<IReservationRepository> _mockReservationRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<ITableRepository> _mockTableRepository = new();
    private readonly Mock<IOrderRepository> _mockOrderRepository = new();
    private readonly GuestSessionService _guestSessionService;

    public GuestSessionServiceTests()
    {
        _guestSessionService = new GuestSessionService(
            _mockGroupReservationRepository.Object,
            _mockGuestSessionRepository.Object,
            _mockDishRepository.Object,
            _mockReservationRepository.Object,
            _mockUserRepository.Object,
            _mockTableRepository.Object,
            _mockOrderRepository.Object);
    }
    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(3)]
    public async Task CreateGuestSessionAsync_CorrectData_ReturnsSessionToken(
        int guestCount,
        CreateGuestSessionRequest request,
        GroupReservation groupReservation,
        Reservation parentReservation)
    {
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = parentReservation.Id;

        parentReservation.GuestCount = 5;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(parentReservation.Id))
            .ReturnsAsync(parentReservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([]);

        GuestSession? session = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => session = s);

        _mockGuestSessionRepository
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(guestCount);

        var response = await _guestSessionService.CreateGuestSessionAsync(request);

        Assert.NotEmpty(response.SessionId);
        Assert.NotEmpty(response.SessionToken);
        Assert.NotNull(session);
        Assert.Equal(groupReservation.GroupReservationId, session.GroupReservationId);
        Assert.Equal(request.GuestName, session.GuestName);
        Assert.NotNull(session.SessionTokenHash);
        Assert.NotEmpty(session.SessionTokenHash);
    }

    [Theory]
    [AutoData]
    public async Task CreateGuestSessionAsync_InvalidGroupReservationId_ThrowsGroupReservationNotFoundException(CreateGuestSessionRequest request)
    {
        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(request.GroupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.CreateGuestSessionAsync(request));
    }

    [Theory]
    [AutoData]
    public async Task CreateGuestSessionAsync_GroupReservationExpired_ThrowsGroupReservationExpiredException(CreateGuestSessionRequest request, GroupReservation groupReservation)
    {
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(request.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _guestSessionService.CreateGuestSessionAsync(request));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldAddNewDish_WhenDishDoesNotExistInSession(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, Dish dish)
    {
        guestSession.Dishes = [];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;
        dish.State = DishState.Available;
        int quantityToAdd = 2;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);
        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([dish]);

        await _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantityToAdd);

        Assert.Single(guestSession.Dishes);
        Assert.Equal(dish.Id, guestSession.Dishes[0].DishId);
        Assert.Equal(quantityToAdd, guestSession.Dishes[0].Quantity);
        Assert.Equal(OrderItemState.Pending, guestSession.Dishes[0].State);

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldIncreaseQuantity_WhenDishAlreadyExists(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, Dish dish)
    {
        int initialQuantity = 3;
        int quantityToAdd = 2;
        guestSession.Dishes = [new OrderItem { DishId = dish.Id, Quantity = initialQuantity, State = OrderItemState.Pending }];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;
        dish.State = DishState.Available;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);
        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([dish]);

        await _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantityToAdd);

        Assert.Single(guestSession.Dishes);
        Assert.Equal(initialQuantity + quantityToAdd, guestSession.Dishes[0].Quantity);

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(-1)]
    public async Task AddDishAsync_ShouldThrowValidationException_WhenQuantityIsZeroOrLess(int quantity, string guestSessionId, Dish dish)
    {
        dish.State = DishState.Available;

        _mockDishRepository
            .Setup(r => r.GetDishByIdAsync(dish.Id))
            .ReturnsAsync(dish);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.AddDishAsync(guestSessionId, dish.Id, quantity));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldThrowGuestSessionNotFoundException_WhenSessionNotFound(string guestSessionId, Dish dish)
    {
        dish.State = DishState.Available;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        _mockDishRepository
            .Setup(r => r.GetDishByIdAsync(dish.Id))
            .ReturnsAsync(dish);

        var exception = await Assert.ThrowsAsync<GuestSessionNotFoundException>(() =>
            _guestSessionService.AddDishAsync(guestSessionId, dish.Id, 2));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldThrowDishNotFound_WhenDishNotFound(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, string dishId)
    {
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);

        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.AddDishAsync(guestSession.Id, dishId, quantity: 2));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldThrowDishNotAvailable_WhenDishIsNotAvailable(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, Dish dish)
    {
        dish.State = DishState.OutOfStock;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);
        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([dish]);

        var exception = await Assert.ThrowsAsync<DishIsNotAvailableException>(() =>
            _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantity: 2));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task AddDishAsync_ShouldThrowGroupReservationExpired_WhenGroupStatusIsNotOpen(GroupReservationStatus status, Type exceptionType, GuestSession guestSession, GroupReservation groupReservation, Dish dish)
    {
        dish.State = DishState.Available;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = status;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockDishRepository
            .Setup(r => r.GetDishByIdAsync(dish.Id))
            .ReturnsAsync(dish);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        var exception = await Assert.ThrowsAsync(exceptionType, () =>
            _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantity: 2));
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldDecreaseQuantity_WhenQuantityIsGreaterThanAmountToRemove(GuestSession guestSession, GroupReservation groupReservation, string dishId)
    {
        int initialQuantity = 5;
        int quantityToRemove = 2;
        guestSession.Dishes =
        [
            new OrderItem { DishId = dishId, Quantity = initialQuantity, State = OrderItemState.Pending }
        ];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);


        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await _guestSessionService.RemoveDishAsync(guestSession.Id, dishId, quantityToRemove);

        Assert.Single(guestSession.Dishes);
        Assert.Equal(initialQuantity - quantityToRemove, guestSession.Dishes[0].Quantity);

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldRemoveDishFromList_WhenQuantityToRemoveIsEqualOrGreater(GuestSession guestSession, GroupReservation groupReservation, string dishId)
    {
        int initialQuantity = 2;
        int quantityToRemove = 3;
        guestSession.Dishes =
        [
            new OrderItem { DishId = dishId, Quantity = initialQuantity, State = OrderItemState.Pending }
        ];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await _guestSessionService.RemoveDishAsync(guestSession.Id, dishId, quantityToRemove);

        Assert.Empty(guestSession.Dishes);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldThrowDishNotFoundException_WhenDishDoesNotExistInSession(GuestSession guestSession, GroupReservation groupReservation, string dishId)
    {
        guestSession.Dishes = [];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
           .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
           .ReturnsAsync(groupReservation);

        var exception = await Assert.ThrowsAsync<DishNotFoundException>(() =>
            _guestSessionService.RemoveDishAsync(guestSession.Id, dishId, 2));

        Assert.Contains("not found in guest session", exception.Message);
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldThrowValidationException_WhenQuantityIsZeroOrLess(string guestSessionId, string dishId)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.RemoveDishAsync(guestSessionId, dishId, 0));

        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.RemoveDishAsync(guestSessionId, dishId, -5));
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldThrowGuestSessionNotFoundException_WhenSessionNotFound(string guestSessionId, string dishId)
    {
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        var exception = await Assert.ThrowsAsync<GuestSessionNotFoundException>(() =>
            _guestSessionService.RemoveDishAsync(guestSessionId, dishId, 2));
    }
    [Theory]
    [InlineAutoData(1, 0)]
    [InlineAutoData(1, 1)]
    [InlineAutoData(2, 1)]
    [InlineAutoData(2, 2)]
    public async Task CreateGuestSessionAsync_WhenCapacityReached_ThrowsGuestLimitExceededException(
        int reservationCapacity,
        int guestCount,
        CreateGuestSessionRequest request,
        GroupReservation groupReservation,
        Reservation parentReservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.ReservationId = parentReservation.Id;

        parentReservation.GuestCount = reservationCapacity;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(parentReservation.Id))
            .ReturnsAsync(parentReservation);

        _mockGuestSessionRepository
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(guestCount);

        var exception = await Assert.ThrowsAsync<GuestLimitExceededException>(() => _guestSessionService.CreateGuestSessionAsync(request));

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task CreateGuestSessionAsync_MissingParentReservation_ThrowsGroupReservationExpiredException(
        CreateGuestSessionRequest request,
        GroupReservation groupReservation)
    {
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(groupReservation.ReservationId))
            .ReturnsAsync((Reservation?)null);

        var exception = await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _guestSessionService.CreateGuestSessionAsync(request));
    }

    [Theory]
    [AutoData]
    public async Task IsAuthorizedAsync_ValidTokenAndId_ReturnsTrue(GuestSession guestSession, byte[] tokenBytes)
    {
        string tokenString = Convert.ToBase64String(tokenBytes);
        var hashString = Convert.ToBase64String(SHA256.HashData(tokenBytes));
        guestSession.SessionTokenHash = hashString;

        _mockGuestSessionRepository
            .Setup(r => r.GetByTokenHash(hashString))
            .ReturnsAsync(guestSession);

        bool result = await _guestSessionService.IsAuthorizedAsync(tokenString, guestSession.Id);

        Assert.True(result);
    }

    [Theory]
    [AutoData]
    public async Task IsAuthorizedAsync_InvalidToken_ReturnsFalse(byte[] tokenBytes, string guestSessionId)
    {
        string tokenString = Convert.ToBase64String(tokenBytes);
        var hashString = Convert.ToBase64String(SHA256.HashData(tokenBytes));

        _mockGuestSessionRepository
            .Setup(r => r.GetByTokenHash(hashString))
            .ReturnsAsync((GuestSession?)null);

        bool result = await _guestSessionService.IsAuthorizedAsync(tokenString, guestSessionId);

        Assert.False(result);
    }

    [Theory]
    [AutoData]
    public async Task IsAuthorizedAsync_InvalidGuestSessionId_ReturnsFalse(GuestSession guestSession, byte[] tokenBytes, string guestSessionId)
    {
        string tokenString = Convert.ToBase64String(tokenBytes);
        var hashString = Convert.ToBase64String(SHA256.HashData(tokenBytes));
        guestSession.SessionTokenHash = hashString;

        _mockGuestSessionRepository
            .Setup(r => r.GetByTokenHash(hashString))
            .ReturnsAsync(guestSession);

        bool result = await _guestSessionService.IsAuthorizedAsync(tokenString, guestSessionId);

        Assert.False(result);
    }
    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_Valid_SubmitsSuccessfuilly(GuestSession guestSession, GroupReservation groupReservation, Dish[] dishes)
    {
        foreach (var dish in dishes)
            dish.State = DishState.Available;

        guestSession.IsSubmitted = false;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
           .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
           .ReturnsAsync(groupReservation);

        _mockDishRepository
            .Setup(d => d.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(dishes);

        GuestSession? session = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => session = s);

        await _guestSessionService.SubmitCartAsync(guestSession.Id);

        Assert.NotNull(session);
        Assert.True(session.IsSubmitted);
    }

    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_SessionNotFound_ThrowsGuestSessionNotFound(string guestSessionId)
    {
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => _guestSessionService.SubmitCartAsync(guestSessionId));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task SubmitCartAsync_GroupReservationNotOpen_ThrowsException(GroupReservationStatus status, Type exceptionType, GuestSession guestSession, GroupReservation groupReservation)
    {
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = status;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(exceptionType, () => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_GuestSessionHasNoDishes_ThrowsCartIsEmptyException(GuestSession guestSession, GroupReservation groupReservation)
    {
        guestSession.Dishes = [];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<CartIsEmptyException>(() => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_GuestSessionSubmitted_ThrowsAlreadySubmited(GuestSession guestSession, GroupReservation groupReservation)
    {
        guestSession.IsSubmitted = true;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GuestSessionSubmittedException>(() => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }

    [Theory]
    [InlineAutoData(DishState.OutOfStock)]
    [InlineAutoData(DishState.OnStop)]
    public async Task SubmitCartAsync_DishNotAvailable_ThrowsDishNotAvailable(DishState state, Dish dish, GuestSession guestSession, GroupReservation groupReservation)
    {
        dish.State = state;
        guestSession.IsSubmitted = false;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        guestSession.Dishes = 
        [
            new OrderItem()
            {
                DishId = dish.Id,
                Quantity = 1,
            }
        ];

        _mockDishRepository
            .Setup(d => d.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([dish]);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
           .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
           .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<DishIsNotAvailableException>(() => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }
    [Theory]
    [MemberAutoData(nameof(PepareDishesForGuestSession))]
    public async Task GetGuestSessionInfoAsync_Guest_ReturnsAsync(
        IEnumerable<string> dishIds,
        IEnumerable<Dish> dishes,
        List<OrderItem> orderItems,
        decimal expectedTotal,
        GuestSession session)
    {
        session.Dishes = orderItems;
        session.IsOwnerSession = false;

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(dishIds))
            .ReturnsAsync(dishes);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(session.Id))
            .ReturnsAsync(session);

        GetGuestSessionResponse result = await _guestSessionService.GetGuestSessionInfoAsync(session.Id);

        Assert.Equal(session.GuestName, result.Name);
        Assert.Equal(dishes.Count(), result.Items.Count());
        Assert.Equal(expectedTotal, result.Total);
    }

    [Theory]
    [MemberAutoData(nameof(PepareDishesForGuestSession))]
    public async Task GetGuestSessionInfoAsync_GroupOwner_ReturnsAsync(
        IEnumerable<string> dishIds,
        IEnumerable<Dish> dishes,
        List<OrderItem> orderItems,
        decimal expectedTotal,
        GuestSession session,
        GroupReservation groupReservation,
        Reservation reservation,
        string customerId,
        string ownerName,
        UserType userType)
    {
        session.Dishes = orderItems;
        session.IsOwnerSession = true;
        session.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.ReservationId = reservation.Id;
        reservation.CustomerId = customerId;
        userType.Attributes.Add(new AttributeType()
        {
            Name = "given_name",
            Value = ownerName,
        });

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(dishIds))
            .ReturnsAsync(dishes);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(session.Id))
            .ReturnsAsync(session);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(customerId))
            .ReturnsAsync(userType);

        GetGuestSessionResponse result = await _guestSessionService.GetGuestSessionInfoAsync(session.Id);

        Assert.Equal(ownerName, result.Name);
        Assert.Equal(dishes.Count(), result.Items.Count());
        Assert.Equal(expectedTotal, result.Total);
    }

    public static TheoryData<IEnumerable<string>, IEnumerable<Dish>, List<OrderItem>, decimal> PepareDishesForGuestSession()
    {
        var dishMap = new Dictionary<string, Dish>()
        {
            ["1"] = new()
            {
                Id = "1",
                Price = 1
            },
            ["2"] = new()
            {
                Id = "2",
                Price = 2
            },
            ["3"] = new()
            {
                Id = "3",
                Price = 3
            }
        };

        var orderItems = new List<OrderItem>
        {
            new OrderItem()
                    {
                        DishId = "1",
                        Quantity = 2
                    },
                    new OrderItem()
                    {
                        DishId = "2",
                        Quantity = 3
                    },
                    new OrderItem()
                    {
                        DishId = "3",
                        Quantity = 1
                    }
        };

        var expectedTotal = orderItems.Sum(o => dishMap[o.DishId].Price * o.Quantity);

        return new()
        {
            { dishMap.Keys, dishMap.Values, orderItems, expectedTotal }
        };
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_Owner_ReturnsOwnerFirstThenSubmittedGuests(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string ownerName,
        UserType userType)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        userType.Attributes.Add(new AttributeType { Name = "given_name", Value = ownerName });
        _mockUserRepository.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(userType);

        var salad = new Dish { Id = "dish1", Name = "Caesar Salad", Price = 180 };
        var soup = new Dish { Id = "dish2", Name = "Tom Yum", Price = 220 };
        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([salad, soup]);

        var draftGuest = CreateSession("s-olena", groupReservation.GroupReservationId, "Olena", isSubmitted: false, (soup.Id, 1));
        var submittedGuest = CreateSession("s-andrii", groupReservation.GroupReservationId, "Andrii", isSubmitted: true, (salad.Id, 2));
        var ownerSession = CreateSession("owner-" + groupReservation.GroupReservationId, groupReservation.GroupReservationId, "Owner", isSubmitted: false);
        ownerSession.IsOwnerSession = true;

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([draftGuest, ownerSession, submittedGuest]);
        SetupOwnerOrder(reservation, (soup.Id, 1, OrderItemState.Pending));

        GetAllSessionsResponse result = await _guestSessionService.GetGroupSessionsAsync(userId, groupReservation.GroupReservationId);

        var participants = result.Participants.ToList();
        Assert.Equal(3, participants.Count);
        Assert.Equal(ParticipantType.Owner, participants[0].Type);
        Assert.Equal(ownerName, participants[0].Name);
        Assert.Equal(ownerSession.Id, participants[0].Id);
        Assert.Equal(220, participants[0].Total);
        Assert.True(participants[0].IsSubmitted);
        Assert.Equal(("s-andrii", ParticipantType.Guest, 360m, true), (participants[1].Id, participants[1].Type, participants[1].Total, participants[1].IsSubmitted));
        Assert.Equal(("s-olena", ParticipantType.Guest, 220m, false), (participants[2].Id, participants[2].Type, participants[2].Total, participants[2].IsSubmitted));
        Assert.Equal(800, result.Total);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_NoOrderNoOwnerSession_ReturnsOwnerWithoutDishesAndStableId(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([]);
        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([]);

        GetAllSessionsResponse result = await _guestSessionService.GetGroupSessionsAsync(userId, groupReservation.GroupReservationId);

        var owner = Assert.Single(result.Participants);
        Assert.Equal(ParticipantType.Owner, owner.Type);
        Assert.Equal("owner-" + groupReservation.GroupReservationId, owner.Id);
        Assert.Equal("Owner", owner.Name);
        Assert.Empty(owner.Items);
        Assert.Equal(0, result.Total);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_DishRemovedFromMenu_SkipsItem(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        var salad = new Dish { Id = "dish1", Name = "Caesar Salad", Price = 180 };
        var guest = CreateSession("s1", groupReservation.GroupReservationId, "Andrii", isSubmitted: true, (salad.Id, 1), ("removed-dish", 3));

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([guest]);
        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([salad]);

        GetAllSessionsResponse result = await _guestSessionService.GetGroupSessionsAsync(userId, groupReservation.GroupReservationId);

        var guestParticipant = result.Participants.Single(p => p.Type == ParticipantType.Guest);
        Assert.Single(guestParticipant.Items);
        Assert.Equal(180, result.Total);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_NotOwner_ThrowsGroupReservationNotFoundException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string otherUserId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.GetGroupSessionsAsync(otherUserId, groupReservation.GroupReservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_GroupNotFound_ThrowsGroupReservationNotFoundException(string userId, string groupReservationId)
    {
        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.GetGroupSessionsAsync(userId, groupReservationId));
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_ValidInput_DeletesSessionAndReturnsSnapshot(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        var removed = CreateSession("s1", groupReservation.GroupReservationId, "Andrii", isSubmitted: true);
        var remaining = CreateSession("s2", groupReservation.GroupReservationId, "Olena", isSubmitted: false);

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(removed.Id)).ReturnsAsync(removed);
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([remaining]);
        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([]);

        GetAllSessionsResponse result = await _guestSessionService.DeleteGuestSessionAsync(userId, groupReservation.GroupReservationId, removed.Id);

        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(removed), Times.Once);
        Assert.Equal([ParticipantType.Owner, ParticipantType.Guest], result.Participants.Select(p => p.Type));
        Assert.Equal(remaining.Id, result.Participants.Last().Id);
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_SessionNotFound_ThrowsGuestSessionNotFoundException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string guestSessionId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSessionId)).ReturnsAsync((GuestSession?)null);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => _guestSessionService.DeleteGuestSessionAsync(userId, groupReservation.GroupReservationId, guestSessionId));
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_SessionFromAnotherGroup_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string otherGroupId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        var session = CreateSession("s1", otherGroupId, "Andrii", isSubmitted: false);
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(session.Id)).ReturnsAsync(session);

        await Assert.ThrowsAsync<ValidationException>(() => _guestSessionService.DeleteGuestSessionAsync(userId, groupReservation.GroupReservationId, session.Id));
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_OwnerSession_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        var ownerSession = CreateSession("owner-" + groupReservation.GroupReservationId, groupReservation.GroupReservationId, "Owner", isSubmitted: false);
        ownerSession.IsOwnerSession = true;
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(ownerSession.Id)).ReturnsAsync(ownerSession);

        await Assert.ThrowsAsync<ValidationException>(() => _guestSessionService.DeleteGuestSessionAsync(userId, groupReservation.GroupReservationId, ownerSession.Id));
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task DeleteGuestSessionAsync_GroupNotOpen_ThrowsAndDoesNotDelete(
        GroupReservationStatus status,
        Type expectedException,
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        groupReservation.Status = status;
        var session = CreateSession("s1", groupReservation.GroupReservationId, "Andrii", isSubmitted: true);
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(session.Id)).ReturnsAsync(session);

        await Assert.ThrowsAsync(expectedException, () => _guestSessionService.DeleteGuestSessionAsync(userId, groupReservation.GroupReservationId, session.Id));
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task DeleteGuestSessionAsync_NotOwner_ThrowsGroupReservationNotFoundException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string otherUserId,
        string guestSessionId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.DeleteGuestSessionAsync(otherUserId, groupReservation.GroupReservationId, guestSessionId));
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task GetOwnerSessionIdAsync_NoOwnerSession_CreatesOwnerSession(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        reservation.CustomerId = userId;
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _mockGroupReservationRepository.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync(groupReservation);
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(It.IsAny<string>())).ReturnsAsync((GuestSession?)null);

        GuestSession? saved = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => saved = s);

        string result = await _guestSessionService.GetOwnerSessionIdAsync(userId, reservation.Id);

        Assert.NotNull(saved);
        Assert.Equal(saved.Id, result);
        Assert.Equal("owner-" + groupReservation.GroupReservationId, result);
        Assert.True(saved.IsOwnerSession);
        Assert.Equal(userId, saved.OwnerUserId);
        Assert.Equal(groupReservation.GroupReservationId, saved.GroupReservationId);
        Assert.Empty(saved.Dishes);
    }

    [Theory]
    [AutoData]
    public async Task GetOwnerSessionIdAsync_OwnerSessionExists_ReturnsItWithoutSaving(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        reservation.CustomerId = userId;
        var ownerSession = CreateSession("owner-" + groupReservation.GroupReservationId, groupReservation.GroupReservationId, "Owner", isSubmitted: false);
        ownerSession.IsOwnerSession = true;

        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _mockGroupReservationRepository.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync(groupReservation);
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(ownerSession.Id)).ReturnsAsync(ownerSession);

        string result = await _guestSessionService.GetOwnerSessionIdAsync(userId, reservation.Id);

        Assert.Equal(ownerSession.Id, result);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task GetOwnerSessionIdAsync_NotOwner_ThrowsReservationNotFoundException(Reservation reservation, string otherUserId)
    {
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _guestSessionService.GetOwnerSessionIdAsync(otherUserId, reservation.Id));
    }

    [Theory]
    [AutoData]
    public async Task GetOwnerSessionIdAsync_ReservationNotFound_ThrowsReservationNotFoundException(string userId, string reservationId)
    {
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(reservationId)).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _guestSessionService.GetOwnerSessionIdAsync(userId, reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetOwnerSessionIdAsync_NoGroupReservation_ThrowsGroupReservationNotFoundException(Reservation reservation, string userId)
    {
        reservation.CustomerId = userId;
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _mockGroupReservationRepository.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.GetOwnerSessionIdAsync(userId, reservation.Id));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationStatusAsync_OwnerSessionExists_CountsOwnerOnce(
        GroupReservation groupReservation,
        Reservation reservation)
    {
        groupReservation.ReservationId = reservation.Id;
        var ownerSession = CreateSession("owner-" + groupReservation.GroupReservationId, groupReservation.GroupReservationId, "Owner", isSubmitted: false);
        ownerSession.IsOwnerSession = true;
        var guest = CreateSession("s1", groupReservation.GroupReservationId, "Andrii", isSubmitted: false);

        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([ownerSession, guest]);

        var result = await _guestSessionService.GetGroupReservationStatusAsync(groupReservation.GroupReservationId);

        Assert.Equal(2, result.CurrentParticipants);
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_SubmittedGuest_SetsQuantityAndReturnsSnapshot(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        var session = CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d1", 2));
        SetupSessions(groupReservation, session);
        SetupDish("d1", DishState.Available);

        GetAllSessionsResponse result = await _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, session.Id, "d1", 3);

        Assert.Equal(3, session.Dishes.Single().Quantity);
        Assert.True(session.IsSubmitted);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(session), Times.Once);
        Assert.Equal(session.Id, result.Participants.Last().Id);
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_ZeroQuantity_RemovesDishAndEmptiedGuestIsNoLongerSubmitted(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        var session = CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d1", 2));
        SetupSessions(groupReservation, session);

        await _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, session.Id, "d1", 0);

        Assert.Empty(session.Dishes);
        Assert.False(session.IsSubmitted);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(session), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_Owner_EditsOrderAndLineGoesBackToPending(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        SetupSessions(groupReservation, CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d9", 1)));
        var order = SetupOwnerOrder(reservation, ("d1", 2, OrderItemState.Confirmed), ("d2", 1, OrderItemState.Pending));

        GetAllSessionsResponse result = await _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, "owner-" + groupReservation.GroupReservationId, "d1", 1);

        Assert.Equal((1, OrderItemState.Pending), (order.Dishes[0].Quantity, order.Dishes[0].State));
        _mockOrderRepository.Verify(r => r.SaveOrderAsync(order), Times.Once);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
        Assert.Equal(ParticipantType.Owner, result.Participants.First().Type);
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_OwnerZeroQuantity_RemovesDishFromOrder(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        SetupSessions(groupReservation, CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d9", 1)));
        var order = SetupOwnerOrder(reservation, ("d1", 2, OrderItemState.Pending), ("d2", 1, OrderItemState.Pending));

        await _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, "owner-" + groupReservation.GroupReservationId, "d2", 0);

        Assert.Equal(["d1"], order.Dishes.Select(d => d.DishId));
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_OwnerWithoutOrder_ThrowsDishNotFoundException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        _mockOrderRepository.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<DishNotFoundException>(() => _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, "owner-" + groupReservation.GroupReservationId, "d1", 1));
        _mockOrderRepository.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_OrderLines_SkipsCanceledLinesAndCancelledOrder(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        SetupSessions(groupReservation, CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: false));
        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([new Dish { Id = "d1", Name = "Soup", Price = 10 }, new Dish { Id = "d2", Name = "Salad", Price = 5 }]);
        var order = SetupOwnerOrder(reservation, ("d1", 1, OrderItemState.Pending), ("d2", 3, OrderItemState.Canceled));

        var owner = (await _guestSessionService.GetGroupSessionsAsync(userId, groupReservation.GroupReservationId)).Participants.First();
        Assert.Equal(["d1"], owner.Items.Select(i => i.Dish.Id));

        order.Status = OrderStatus.Cancelled;
        owner = (await _guestSessionService.GetGroupSessionsAsync(userId, groupReservation.GroupReservationId)).Participants.First();
        Assert.Empty(owner.Items);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupSessionsAsync_AfterSubmit_OwnerDishesExcludeGuestDishesMergedIntoOrder(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        groupReservation.Status = GroupReservationStatus.Submitted;
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([
                CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d1", 2)),
                CreateSession("s2", groupReservation.GroupReservationId, "Luka", isSubmitted: true, ("d2", 1)),
                CreateSession("s3", groupReservation.GroupReservationId, "Giorgi", isSubmitted: false, ("d1", 5)),
            ]);
        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([new Dish { Id = "d1", Name = "Soup", Price = 10 }, new Dish { Id = "d2", Name = "Salad", Price = 5 }]);
        // Owner ordered d1 × 1; submit added Nino's d1 × 2 and Luka's d2 × 1 to the same Order.
        SetupOwnerOrder(reservation, ("d1", 3, OrderItemState.Pending), ("d2", 1, OrderItemState.Pending));

        var owner = (await _guestSessionService.GetGroupSessionsAsync(userId, groupReservation.GroupReservationId)).Participants.First();

        var item = Assert.Single(owner.Items);
        Assert.Equal(("d1", 1), (item.Dish.Id, item.Quantity));
        Assert.Equal(10, owner.Total);
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_GuestNotSubmitted_ThrowsValidationExceptionAndDoesNotSave(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        var session = CreateSession("s1", groupReservation.GroupReservationId, "Giorgi", isSubmitted: false, ("d1", 1));
        SetupSessions(groupReservation, session);

        await Assert.ThrowsAsync<ValidationException>(() => _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, session.Id, "d1", 2));
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_DishNotInCart_ThrowsDishNotFoundException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        var session = CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d1", 1));
        SetupSessions(groupReservation, session);

        await Assert.ThrowsAsync<DishNotFoundException>(() => _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, session.Id, "other", 2));
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_IncreaseUnavailableDish_ThrowsButDecreaseIsAllowed(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        var session = CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d1", 2));
        SetupSessions(groupReservation, session);
        SetupDish("d1", DishState.OnStop);

        await Assert.ThrowsAsync<DishIsNotAvailableException>(() => _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, session.Id, "d1", 3));
        await _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, session.Id, "d1", 1);

        Assert.Equal(1, session.Dishes.Single().Quantity);
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task UpdateSessionDishQuantityAsync_GroupNotOpen_ThrowsAndDoesNotSave(
        GroupReservationStatus status,
        Type expectedException,
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        groupReservation.Status = status;
        var session = CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d1", 1));
        SetupSessions(groupReservation, session);

        await Assert.ThrowsAsync(expectedException, () => _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, session.Id, "d1", 2));
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_AfterCutOff_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        reservation.StartTime = DateTime.UtcNow.AddMinutes(20);
        var session = CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d1", 1));
        SetupSessions(groupReservation, session);

        await Assert.ThrowsAsync<ValidationException>(() => _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, session.Id, "d1", 0));
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_SessionFromAnotherGroup_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string otherGroupId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        var session = CreateSession("s1", otherGroupId, "Nino", isSubmitted: true, ("d1", 1));
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(session.Id)).ReturnsAsync(session);

        await Assert.ThrowsAsync<ValidationException>(() => _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservation.GroupReservationId, session.Id, "d1", 2));
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_NotOwner_ThrowsGroupReservationNotFoundException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string otherUserId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.UpdateSessionDishQuantityAsync(otherUserId, groupReservation.GroupReservationId, "s1", "d1", 2));
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task UpdateSessionDishQuantityAsync_NegativeQuantity_ThrowsValidationException(string userId, string groupReservationId)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservationId, "s1", "d1", -1));
    }

    [Theory]
    [AutoData]
    public async Task RemoveEmptySeatAsync_FreeSeat_LowersGuestCountAndReturnsStatus(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        reservation.GuestCount = 4;
        SetupJoinedGuests(groupReservation, 2);

        GroupReservationStatusResponse result = await _guestSessionService.RemoveEmptySeatAsync(userId, groupReservation.GroupReservationId);

        Assert.Equal(3, reservation.GuestCount);
        _mockReservationRepository.Verify(r => r.SaveReservationAsync(reservation), Times.Once);
        Assert.Equal(3, result.MaxCapacity);
        Assert.Equal(3, result.CurrentParticipants);
        Assert.Equal(GroupReservationStatus.Open, result.Status);
    }

    [Theory]
    [AutoData]
    public async Task RemoveEmptySeatAsync_EverySeatTaken_ThrowsValidationExceptionAndDoesNotSave(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        reservation.GuestCount = 3;
        SetupJoinedGuests(groupReservation, 2);

        await Assert.ThrowsAsync<ValidationException>(() => _guestSessionService.RemoveEmptySeatAsync(userId, groupReservation.GroupReservationId));
        Assert.Equal(3, reservation.GuestCount);
        _mockReservationRepository.Verify(r => r.SaveReservationAsync(It.IsAny<Reservation>()), Times.Never);
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task RemoveEmptySeatAsync_GroupNotOpen_ThrowsAndDoesNotSave(
        GroupReservationStatus status,
        Type expectedException,
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        groupReservation.Status = status;
        reservation.GuestCount = 4;
        SetupJoinedGuests(groupReservation, 1);

        await Assert.ThrowsAsync(expectedException, () => _guestSessionService.RemoveEmptySeatAsync(userId, groupReservation.GroupReservationId));
        _mockReservationRepository.Verify(r => r.SaveReservationAsync(It.IsAny<Reservation>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task RemoveEmptySeatAsync_AfterCutOff_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);
        reservation.StartTime = DateTime.UtcNow.AddMinutes(20);
        reservation.GuestCount = 4;
        SetupJoinedGuests(groupReservation, 1);

        await Assert.ThrowsAsync<ValidationException>(() => _guestSessionService.RemoveEmptySeatAsync(userId, groupReservation.GroupReservationId));
        _mockReservationRepository.Verify(r => r.SaveReservationAsync(It.IsAny<Reservation>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task RemoveEmptySeatAsync_NotOwner_ThrowsGroupReservationNotFoundException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string otherUserId)
    {
        SetupEditableGroup(groupReservation, reservation, userId);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.RemoveEmptySeatAsync(otherUserId, groupReservation.GroupReservationId));
        _mockReservationRepository.Verify(r => r.SaveReservationAsync(It.IsAny<Reservation>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task GetParticipantCountByReservationIdAsync_GroupExists_CountsGuestsAndOwner(GroupReservation groupReservation, string reservationId)
    {
        _mockGroupReservationRepository.Setup(r => r.GetByReservationIdAsync(reservationId)).ReturnsAsync(groupReservation);
        SetupJoinedGuests(groupReservation, 2);

        Assert.Equal(3, await _guestSessionService.GetParticipantCountByReservationIdAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetParticipantCountByReservationIdAsync_NoGroup_ReturnsNull(string reservationId)
    {
        _mockGroupReservationRepository.Setup(r => r.GetByReservationIdAsync(reservationId)).ReturnsAsync((GroupReservation?)null);

        Assert.Null(await _guestSessionService.GetParticipantCountByReservationIdAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupPreOrderSummaryAsync_OneGuest_ReturnsNull(Reservation reservation)
    {
        reservation.GuestCount = 1;

        Assert.Null(await _guestSessionService.GetGroupPreOrderSummaryAsync(reservation, null));
        _mockGroupReservationRepository.Verify(r => r.GetByReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupPreOrderSummaryAsync_NoGroup_ReturnsNull(Reservation reservation)
    {
        reservation.GuestCount = 4;
        _mockGroupReservationRepository.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync((GroupReservation?)null);

        Assert.Null(await _guestSessionService.GetGroupPreOrderSummaryAsync(reservation, null));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupPreOrderSummaryAsync_CountsSubmittedGuestsAndOwnerWithDishes(
        GroupReservation groupReservation,
        Reservation reservation)
    {
        reservation.GuestCount = 4;
        groupReservation.Status = GroupReservationStatus.Open;
        _mockGroupReservationRepository.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync(groupReservation);
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([
                CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d1", 1)),
                CreateSession("s2", groupReservation.GroupReservationId, "Giorgi", isSubmitted: false, ("d1", 2)),
            ]);
        var order = SetupOwnerOrder(reservation, ("d2", 1, OrderItemState.Pending));

        var summary = await _guestSessionService.GetGroupPreOrderSummaryAsync(reservation, order);

        Assert.NotNull(summary);
        Assert.Equal((GroupReservationStatus.Open, 2, 3, 4), (summary.Status, summary.Submitted, summary.Joined, summary.Seats));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupPreOrderSummaryAsync_OwnerWithoutOwnDishes_IsNotCountedAfterSubmit(
        GroupReservation groupReservation,
        Reservation reservation)
    {
        reservation.GuestCount = 2;
        groupReservation.Status = GroupReservationStatus.Submitted;
        _mockGroupReservationRepository.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync(groupReservation);
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([
                CreateSession("s1", groupReservation.GroupReservationId, "Nino", isSubmitted: true, ("d1", 2)),
                CreateSession("s2", groupReservation.GroupReservationId, "Luka", isSubmitted: true, ("d1", 1)),
            ]);
        // The Order holds only what the guests' submit merged in — the Owner ordered nothing themselves.
        var order = SetupOwnerOrder(reservation, ("d1", 3, OrderItemState.Pending));

        var summary = await _guestSessionService.GetGroupPreOrderSummaryAsync(reservation, order);

        Assert.NotNull(summary);
        Assert.Equal((2, 3, 3), (summary.Submitted, summary.Joined, summary.Seats));
    }

    /// <summary>Joined guests plus the Owner's own session, which never counts as a guest.</summary>
    private void SetupJoinedGuests(GroupReservation groupReservation, int guests)
    {
        var ownerSession = CreateSession("owner-" + groupReservation.GroupReservationId, groupReservation.GroupReservationId, "Owner", isSubmitted: false);
        ownerSession.IsOwnerSession = true;
        var sessions = Enumerable.Range(1, guests)
            .Select(i => CreateSession($"s{i}", groupReservation.GroupReservationId, $"Guest {i}", isSubmitted: false))
            .Append(ownerSession)
            .ToList();
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(sessions);
    }

    private Order SetupOwnerOrder(Reservation reservation, params (string DishId, int Quantity, OrderItemState State)[] dishes)
    {
        var order = new Order
        {
            OrderId = "order-" + reservation.Id,
            CustomerId = reservation.CustomerId!,
            ReservationId = reservation.Id,
            ReservationAddress = "48 Rustaveli Avenue, Table 1",
            ReservationTime = "Oct 17, 2026, 12:00 - 13:30",
            Status = OrderStatus.Created,
            Dishes = dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity, State = d.State }).ToList(),
        };
        _mockOrderRepository.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync(order);
        return order;
    }

    private void SetupEditableGroup(GroupReservation groupReservation, Reservation reservation, string userId)
    {
        SetupOwnedGroup(groupReservation, reservation, userId);
        reservation.StartTime = DateTime.UtcNow.AddDays(1);
    }

    private void SetupSessions(GroupReservation groupReservation, GuestSession session)
    {
        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(session.Id)).ReturnsAsync(session);
        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([session]);
    }

    private void SetupDish(string dishId, DishState state)
    {
        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([new Dish { Id = dishId, Name = dishId, State = state }]);
    }

    private void SetupOwnedGroup(GroupReservation groupReservation, Reservation reservation, string userId)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.CustomerId = userId;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
    }

    private static GuestSession CreateSession(string id, string groupReservationId, string name, bool isSubmitted, params (string DishId, int Quantity)[] dishes) => new()
    {
        Id = id,
        GroupReservationId = groupReservationId,
        GuestName = name,
        IsSubmitted = isSubmitted,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity }).ToList(),
    };
}





