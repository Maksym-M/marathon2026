using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Handlers.Reservations;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using APIHandler.Services.Reservations;
using Moq;

namespace APIHandler.Tests.Handlers;

public class GetReservationByIdHandlerTests
{
    private const string OwnerId = "owner-1";

    private readonly Mock<IReservationService> _reservationService = new();
    private readonly Mock<ILocationService> _locationService = new();
    private readonly Mock<IOrderService> _orderService = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly GetReservationByIdHandler _handler;

    public GetReservationByIdHandlerTests()
    {
        _context.Setup(c => c.Logger).Returns(Mock.Of<ILambdaLogger>());
        _locationService
            .Setup(s => s.GetLocationByTableIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new Location { Id = "loc-1", Address = "48 Rustaveli Avenue", Description = "", AverageOccupancy = "" });
        _handler = new GetReservationByIdHandler(_orderService.Object, _locationService.Object, _reservationService.Object);
    }

    [Theory]
    [InlineData(OwnerId, "[Customer]", 200)]
    [InlineData("someone-else", "[Customer]", 404)]
    [InlineData("waiter-7", "[Waiter]", 200)]
    [InlineData("waiter-7", "[\"Waiter\"]", 200)]
    [InlineData("admin-1", "Admin", 200)]
    public async Task HandleRequest_OnlyOwnerOrStaffCanReadACustomersReservation(string callerId, string groups, int expectedStatus)
    {
        SetupReservation(customerId: OwnerId);

        var response = await _handler.HandleRequest(Request(callerId, groups), _context.Object);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_SomeoneElsesReservation_AnswersLikeAMissingOne()
    {
        SetupReservation(customerId: OwnerId);
        var foreign = await _handler.HandleRequest(Request("someone-else", "[Customer]"), _context.Object);

        _reservationService.Setup(s => s.GetReservationByIdAsync("res-1")).ReturnsAsync((Reservation?)null);
        var missing = await _handler.HandleRequest(Request("someone-else", "[Customer]"), _context.Object);

        Assert.Equal((missing.StatusCode, missing.Body), (foreign.StatusCode, foreign.Body));
    }

    [Fact]
    public async Task HandleRequest_AnonymousReservation_StaysReadable()
    {
        SetupReservation(customerId: null);

        var response = await _handler.HandleRequest(Request("any-customer", "[Customer]"), _context.Object);

        Assert.Equal(200, response.StatusCode);
    }

    private void SetupReservation(string? customerId) =>
        _reservationService
            .Setup(s => s.GetReservationByIdAsync("res-1"))
            .ReturnsAsync(new Reservation
            {
                Id = "res-1",
                TableId = "table-1",
                CustomerId = customerId,
                WaiterId = "waiter-7",
                GuestCount = 2,
                Status = ReservationStatus.Reserved,
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            });

    private static APIGatewayProxyRequest Request(string callerId, string groups) => new()
    {
        PathParameters = new Dictionary<string, string> { ["reservationId"] = "res-1" },
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = new Dictionary<string, string> { ["sub"] = callerId, ["cognito:groups"] = groups },
            },
        },
    };
}
