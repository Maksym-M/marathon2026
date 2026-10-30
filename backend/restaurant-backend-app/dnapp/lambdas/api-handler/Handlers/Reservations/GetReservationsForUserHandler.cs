using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Helpers;
using APIHandler.Mappers;
using APIHandler.Models.Reservations;
using APIHandler.Models.Users;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using APIHandler.Services.Reservations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Handlers.Reservations;

public class GetReservationsForUserHandler : IApiRequestHandler
{
    private static readonly string ReservationsTableName = ResourceNames.ReservationsTableName;
    private static readonly string ReservationsCustomerIdGlobalIndex = ResourceNames.ReservationsCustomerIdGlobalIndex;

    private readonly IReservationRepository _reservationRepository;
    private readonly ILocationService _locationService;
    private readonly IOrderService _orderService;
    private readonly IGroupReservationRepository _groupReservationRepository;
    private readonly IGuestSessionService _guestSessionService;
    private readonly ITableRepository _tableRepository;
    private readonly IReservationService _reservationService;

    public GetReservationsForUserHandler(
        IReservationRepository reservationRepository,
        ILocationService locationService,
        IOrderService orderService,
        IGroupReservationRepository groupReservationRepository,
        IGuestSessionService guestSessionService,
        ITableRepository tableRepository,
        IReservationService reservationService)
    {
        _reservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
        _guestSessionService = guestSessionService ?? throw new ArgumentNullException(nameof(guestSessionService));
        _tableRepository = tableRepository ?? throw new ArgumentNullException(nameof(tableRepository));
        _reservationRepository = reservationRepository ?? throw new ArgumentNullException(nameof(reservationRepository));
        _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _groupReservationRepository = groupReservationRepository ?? throw new ArgumentNullException(nameof(groupReservationRepository));
    }

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (request.RequestContext?.Authorizer is null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var userId) ||
            string.IsNullOrEmpty(userId))
        {
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized: User ID not found in request context.", context.Logger);
        }

        if (!request.RequestContext.Authorizer.Claims.TryGetValue("cognito:groups", out var groups) ||
            string.IsNullOrEmpty(groups))
        {
            return ResponseBuilder.CreateErrorResponse(403, "Forbidden: User does not have the required permissions.", context.Logger);
        }

        if (groups.Contains(Roles.Waiter.ToString()))
        {
            return await GetReservationsForWaiterAsync(userId, context);
        }
        else if (groups.Contains(Roles.Customer.ToString()))
        {
            string? scopeValue = null;
            request.QueryStringParameters?.TryGetValue(ReservationScopeHelper.QueryParameter, out scopeValue);
            if (!ReservationScopeHelper.TryParse(scopeValue, out ReservationScope scope))
            {
                return ResponseBuilder.CreateErrorResponse(400, "Invalid 'scope': use 'upcoming' or 'past', or leave it out for all reservations.", context.Logger);
            }

            return await GetReservationsForCustomerAsync(userId, scope, context);
        }
        else
        {
            return ResponseBuilder.CreateErrorResponse(403, "Forbidden: User does not have the required permissions.", context.Logger);
        }
    }

    private async Task<APIGatewayProxyResponse> GetReservationsForWaiterAsync(string userId, ILambdaContext context)
    {
        context.Logger.LogLine($"Querying DynamoDB with index: '{ResourceNames.ReservationsWaiterIdGlobalIndex}' on table: '{ResourceNames.ReservationsTableName}'");

        IEnumerable<Reservation> reservations = await _reservationRepository.GetReservationsByWaiterIdAsync(userId);

        var result = new List<ReservationResponse>();
        foreach (var reservation in reservations)
        {
            try
            {
                var current = await _reservationService.CompleteIfEndedAsync(reservation);
                var response = await ConvertToResponse(current);
                result.Add(response);
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"Error converting reservation {reservation.Id} to response: {ex.Message}");
                return ResponseBuilder.CreateErrorResponse(500, "Internal Server Error: Error while fetching reservation details.", context.Logger);
            }
        }

        return ResponseBuilder.CreateSuccessResponse(result);
    }

    /// <summary>
    /// Building one reservation takes several lookups, so a <paramref name="scope"/> skips the others before them:
    /// the "Reservations" page asks for "Upcoming" first and "Past" only when that tab opens.
    /// </summary>
    private async Task<APIGatewayProxyResponse> GetReservationsForCustomerAsync(string userId, ReservationScope scope, ILambdaContext context)
    {
        try
        {
            var reservations = await _reservationRepository.GetReservationsByCustomerIdAsync(userId);
            var result = new List<ReservationResponse>();

            // Filtered by the status after auto-completion, so a reservation that ended unstarted lands in Past at once.
            // The index can lag a moment behind the table, so the status is checked again on the full item.
            var now = DateTime.UtcNow;
            foreach (var reservation in reservations.Where(reservation =>
                         scope.Includes(ReservationLifecycleHelper.EffectiveStatus(reservation, now))))
            {
                try
                {
                    var fullReservation = await _reservationRepository.GetReservationByIdAsync(reservation.Id) ?? reservation;
                    fullReservation = await _reservationService.CompleteIfEndedAsync(fullReservation);
                    if (!scope.Includes(fullReservation.Status))
                        continue;

                    var response = await ConvertToResponse(fullReservation, includeGroupPreOrder: true);
                    result.Add(response);
                }
                catch (Exception ex)
                {
                    context.Logger.LogLine($"Skipping corrupted reservation {reservation.Id}: {ex.Message}");
                    continue;
                }
            }

            return ResponseBuilder.CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error retrieving reservations for user {userId}: {ex.Message}");
            return ResponseBuilder.CreateErrorResponse(500, "Internal Server Error: Unable to retrieve reservations.", context.Logger);
        }
    }

    private async Task<ReservationResponse> ConvertToResponse(Reservation reservation, bool includeGroupPreOrder = false)
    {
        try
        {
            var location = await _locationService.GetLocationByTableIdAsync(reservation.TableId);
            var reservationOrder = await _orderService.GetOrderByReservationIdAsync(reservation.Id);

            bool guestInviteLinkAvailable = false;
            if (reservation.GuestCount > 1 && reservation.Status != ReservationStatus.Completed)
            {
                GroupReservation? groupReservation = await _groupReservationRepository.GetByReservationIdAsync(reservation.Id);
                if (groupReservation is not null)
                    guestInviteLinkAvailable = groupReservation.Status == GroupReservationStatus.Open;
            }

            var table = await _tableRepository.GetTableAsync(reservation.TableId);
            // US20: the Owner's card shows "Pre-orders: X/Y" and the invite hint from this summary.
            var groupPreOrder = includeGroupPreOrder
                ? await _guestSessionService.GetGroupPreOrderSummaryAsync(reservation, reservationOrder)
                : null;

            return ReservationMapper.ToReservationResponse(
                reservation,
                location.Address,
                reservationOrder,
                guestInviteLinkAvailable: guestInviteLinkAvailable,
                tableNumber: table?.TableNumber,
                groupPreOrder: groupPreOrder);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error converting reservation with Id {reservation.Id}, TableId {reservation.TableId}: {ex.Message}", ex);
        }
    }
}