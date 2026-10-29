using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Handlers.Reservations;
using APIHandler.Helpers;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class GetReservationsForUserHandlerTests
{
    private const string CustomerId = "customer-1";

    private readonly Mock<IReservationRepository> _reservationRepository = new();
    private readonly Mock<ILocationService> _locationService = new();
    private readonly Mock<IOrderService> _orderService = new();
    private readonly Mock<IGroupReservationRepository> _groupReservationRepository = new();
    private readonly Mock<IGuestSessionService> _guestSessionService = new();
    private readonly Mock<ITableRepository> _tableRepository = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly GetReservationsForUserHandler _handler;

    private readonly List<Reservation> _reservations =
    [
        Reservation("reserved", ReservationStatus.Reserved),
        Reservation("in-progress", ReservationStatus.InProgress),
        Reservation("completed", ReservationStatus.Completed),
        Reservation("cancelled", ReservationStatus.Cancelled),
    ];

    public GetReservationsForUserHandlerTests()
    {
        _context.Setup(c => c.Logger).Returns(Mock.Of<ILambdaLogger>());
        _locationService
            .Setup(s => s.GetLocationByTableIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new Location { Id = "loc-1", Address = "48 Rustaveli Avenue", Description = "", AverageOccupancy = "" });
        _reservationRepository.Setup(r => r.GetReservationsByCustomerIdAsync(CustomerId)).ReturnsAsync(_reservations);
        _reservationRepository
            .Setup(r => r.GetReservationByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => _reservations.Single(reservation => reservation.Id == id));
        _handler = new GetReservationsForUserHandler(
            _reservationRepository.Object,
            _locationService.Object,
            _orderService.Object,
            _groupReservationRepository.Object,
            _guestSessionService.Object,
            _tableRepository.Object);
    }

    [Theory]
    [InlineData(null, new[] { "reserved", "in-progress", "completed", "cancelled" })]
    [InlineData("upcoming", new[] { "reserved", "in-progress" })]
    [InlineData("past", new[] { "completed", "cancelled" })]
    [InlineData("Upcoming", new[] { "reserved", "in-progress" })]
    public async Task HandleRequest_Customer_ReturnsOnlyTheRequestedTab(string? scope, string[] expectedIds)
    {
        var response = await _handler.HandleRequest(Request("[Customer]", scope), _context.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(expectedIds, Ids(response));
    }

    [Fact]
    public async Task HandleRequest_Upcoming_SkipsTheLookupsOfPastReservations()
    {
        await _handler.HandleRequest(Request("[Customer]", "upcoming"), _context.Object);

        _reservationRepository.Verify(r => r.GetReservationByIdAsync("completed"), Times.Never);
        _reservationRepository.Verify(r => r.GetReservationByIdAsync("cancelled"), Times.Never);
        _orderService.Verify(s => s.GetOrderByReservationIdAsync("completed"), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_StatusChangedSinceTheIndexWasRead_UsesTheFullItem()
    {
        // The index still says Reserved; the table already has the reservation cancelled.
        _reservationRepository
            .Setup(r => r.GetReservationByIdAsync("reserved"))
            .ReturnsAsync(Reservation("reserved", ReservationStatus.Cancelled));

        var response = await _handler.HandleRequest(Request("[Customer]", "upcoming"), _context.Object);

        Assert.Equal(["in-progress"], Ids(response));
    }

    [Fact]
    public async Task HandleRequest_UnknownScope_Returns400()
    {
        var response = await _handler.HandleRequest(Request("[Customer]", "everything"), _context.Object);

        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData(null, ReservationStatus.Completed, true)]
    [InlineData("", ReservationStatus.Reserved, true)]
    [InlineData("upcoming", ReservationStatus.Reserved, true)]
    [InlineData("upcoming", ReservationStatus.InProgress, true)]
    [InlineData("upcoming", ReservationStatus.Completed, false)]
    [InlineData("upcoming", ReservationStatus.Cancelled, false)]
    [InlineData("past", ReservationStatus.Completed, true)]
    [InlineData("past", ReservationStatus.Cancelled, true)]
    [InlineData("past", ReservationStatus.Reserved, false)]
    [InlineData("past", ReservationStatus.InProgress, false)]
    public void ReservationScope_MatchesTheTabsOfTheReservationsPage(string? value, ReservationStatus status, bool included)
    {
        Assert.True(ReservationScopeHelper.TryParse(value, out ReservationScope scope));
        Assert.Equal(included, scope.Includes(status));
    }

    private static Reservation Reservation(string id, ReservationStatus status) => new()
    {
        Id = id,
        TableId = "table-1",
        CustomerId = CustomerId,
        WaiterId = "waiter-1",
        GuestCount = 1,
        StartTime = new DateTime(2030, 10, 17, 12, 0, 0, DateTimeKind.Utc),
        EndTime = new DateTime(2030, 10, 17, 13, 30, 0, DateTimeKind.Utc),
        Status = status,
    };

    private static APIGatewayProxyRequest Request(string groups, string? scope) => new()
    {
        QueryStringParameters = scope is null ? null : new Dictionary<string, string> { ["scope"] = scope },
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = new Dictionary<string, string> { ["sub"] = CustomerId, ["cognito:groups"] = groups },
            },
        },
    };

    private static string[] Ids(APIGatewayProxyResponse response) =>
        JsonDocument.Parse(response.Body).RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetString()!).ToArray();
}
