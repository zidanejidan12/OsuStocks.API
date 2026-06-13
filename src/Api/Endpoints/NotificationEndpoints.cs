using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Notifications.GetNotifications;
using OsuStocks.Application.Features.Notifications.MarkAllNotificationsRead;
using OsuStocks.Application.Features.Notifications.MarkNotificationRead;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var notificationsGroup = app.MapGroup("/api/v1/notifications")
            .RequireAuthorization()
            .WithTags("Notifications");

        notificationsGroup.MapGet("", async (
            bool? unread,
            int? page,
            int? pageSize,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new GetNotificationsQuery(
                userId,
                unread ?? false,
                page ?? 1,
                pageSize ?? 25), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items,
                page = result.Value.Page,
                pageSize = result.Value.PageSize
            });
        });

        notificationsGroup.MapPost("/{id:guid}/read", async (
            Guid id,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new MarkNotificationReadCommand(userId, id), cancellationToken);
            if (!result.IsSuccess)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new { success = true });
        });

        notificationsGroup.MapPost("/read-all", async (
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new MarkAllNotificationsReadCommand(userId), cancellationToken);
            if (!result.IsSuccess)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new { markedRead = result.Value });
        });
    }
}
