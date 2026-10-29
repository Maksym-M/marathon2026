using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

/// <summary>Owner sets a dish quantity in their own cart or in a submitted guest cart; 0 removes the dish.</summary>
public class UpdateSessionDishQuantityHandler : IApiRequestHandler
{
    private readonly IGuestSessionService _guestSessionService;

    public UpdateSessionDishQuantityHandler(IGuestSessionService guestSessionService)
    {
        _guestSessionService = guestSessionService ?? throw new ArgumentNullException(nameof(guestSessionService));
    }

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out var groupReservationId))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Missing 'groupReservationId' in path parameters.", context.Logger);
        }

        if (!request.TryGetPathValue("guestSessionId", out var guestSessionId))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Missing 'guestSessionId' in path parameters.", context.Logger);
        }

        if (!request.TryGetPathValue("dishId", out var dishId))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Missing 'dishId' in path parameters.", context.Logger);
        }

        if (!TryReadQuantity(request, out int quantity))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Request body must be { \"quantity\": <whole number, 0 or more> }.", context.Logger);
        }

        if (request.RequestContext?.Authorizer?.Claims == null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var userId))
        {
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);
        }

        try
        {
            var updatedSnapshot = await _guestSessionService.UpdateSessionDishQuantityAsync(userId, groupReservationId, guestSessionId, dishId, quantity);
            return ResponseBuilder.CreateJsonResponse(200, updatedSnapshot);
        }
        catch (GroupReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (GuestSessionNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (DishNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (ValidationException ex)
        {
            return ResponseBuilder.CreateErrorCodeResponse(ex);
        }
    }

    private static bool TryReadQuantity(APIGatewayProxyRequest request, out int quantity)
    {
        quantity = 0;
        try
        {
            var body = JsonSerializer.Deserialize<UpdateSessionDishQuantityRequest>(request.Body ?? string.Empty);
            if (body?.Quantity is not int value || value < 0)
                return false;

            quantity = value;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
