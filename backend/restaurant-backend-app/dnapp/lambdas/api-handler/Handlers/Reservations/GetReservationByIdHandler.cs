using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Helpers;
using APIHandler.Entities;
using APIHandler.Mappers;
using APIHandler.Models.Users;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using APIHandler.Services.Reservations;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Handlers.Reservations;

public class GetReservationByIdHandler : IApiRequestHandler
{
    private readonly IReservationService _reservationService;
    private readonly ILocationService _locationService;
    private readonly IOrderService _orderService;

    public GetReservationByIdHandler(IOrderService orderService, ILocationService locationService, IReservationService reservationService)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
        _reservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
    }

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var callerUserId = UserContextHelper.GetAuthenticatedUserId(request);
        if (callerUserId is null)
        {
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized: User ID is missing.", context.Logger);
        }

        if (request.PathParameters == null ||
            !request.PathParameters.TryGetValue("reservationId", out var reservationId) ||
            DataValidationHelper.IsMissing(reservationId))
        {
            return ResponseBuilder.CreateErrorResponse(400, "No reservation id was found in request.", context.Logger);
        }

        var reservation = await _reservationService.GetReservationByIdAsync(reservationId);

        // Another customer's reservation answers like a missing one, so nobody can find out which reservations exist.
        if (reservation == null || !CanRead(request, callerUserId, reservation))
        {
            return ResponseBuilder.CreateErrorResponse(404, $"Reservation with id '{reservationId}' was not found.", context.Logger);
        }

        reservation = await _reservationService.CompleteIfEndedAsync(reservation);

        var location = await _locationService.GetLocationByTableIdAsync(reservation.TableId);

        if (location == null)
        {
            return ResponseBuilder.CreateErrorResponse(404, $"Location with id '{reservation.TableId}' was not found.", context.Logger);
        }

        var order = await _orderService.GetOrderByReservationIdAsync(reservation.Id);

        var reservationResponse = ReservationMapper.ToReservationResponse(reservation, location.Address, order);
        return ResponseBuilder.CreateSuccessResponse(reservationResponse);
    }

    /// <summary>
    /// The reservation's own customer, staff (waiter, admin), or anyone for an anonymous reservation a waiter made
    /// for a walk-in guest (the feedback flow opens those).
    /// </summary>
    private static bool CanRead(APIGatewayProxyRequest request, string callerUserId, Reservation reservation) =>
        reservation.CustomerId is null ||
        reservation.CustomerId == callerUserId ||
        HasGroup(request, Roles.Waiter) ||
        HasGroup(request, Roles.Admin);

    // API Gateway passes "cognito:groups" as "[Waiter]", "[\"Waiter\"]" or "Waiter,Admin" depending on the setup.
    private static bool HasGroup(APIGatewayProxyRequest request, Roles role) =>
        request.RequestContext.Authorizer?.Claims is not null &&
        request.RequestContext.Authorizer.Claims.TryGetValue("cognito:groups", out var groups) &&
        groups.Split(['[', ']', '"', ',', ' '], StringSplitOptions.RemoveEmptyEntries).Contains(role.ToString());
}
