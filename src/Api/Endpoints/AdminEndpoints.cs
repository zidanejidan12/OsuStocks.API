using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Admin.MarketSettings.GetMarketSettings;
using OsuStocks.Application.Features.Admin.MarketSettings.UpdateMarketSettings;
using OsuStocks.Application.Features.PlayerRegistry.AddTrackedPlayer;
using OsuStocks.Application.Features.PlayerRegistry.DisableTrackedPlayer;
using OsuStocks.Application.Features.PlayerRegistry.EnableTrackedPlayer;
using OsuStocks.Application.Features.PlayerRegistry.ListTrackedPlayers;
using OsuStocks.Application.Features.PlayerRegistry.SearchOsuPlayers;
using OsuStocks.Domain.Common.Enums;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/api/v1/admin")
            .RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()))
            .WithTags("Admin");

        adminGroup.MapGet("/market-settings", async (
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetMarketSettingsQuery(), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                ppMultiplier = result.Value.PpMultiplier,
                tradeMultiplier = result.Value.TradeMultiplier,
                decayMultiplier = result.Value.DecayMultiplier,
                isMaintenanceMode = result.Value.IsMaintenanceMode
            });
        });

        adminGroup.MapPut("/market-settings", async (
            UpdateMarketSettingsRequest request,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var actor = ResolveActor(principal);
            var result = await sender.Send(
                new UpdateMarketSettingsCommand(
                    request.PpMultiplier,
                    request.TradeMultiplier,
                    request.DecayMultiplier,
                    request.IsMaintenanceMode,
                    actor),
                cancellationToken);

            if (!result.IsSuccess)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.NoContent();
        });

        var trackedPlayersGroup = adminGroup.MapGroup("/tracked-players");

        trackedPlayersGroup.MapGet("", async (
            bool? isActive,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ListTrackedPlayersQuery(isActive), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new { items = result.Value.Items });
        });

        trackedPlayersGroup.MapGet("/search", async (
            string query,
            int? limit,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new SearchOsuPlayersQuery(query, limit ?? 10), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new { items = result.Value.Items });
        });

        trackedPlayersGroup.MapPost("", async (
            AddTrackedPlayerRequest request,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var actor = ResolveActor(principal);
            var result = await sender.Send(
                new AddTrackedPlayerCommand(request.OsuUserId, request.TrackingTier, actor),
                cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new { trackedPlayerId = result.Value.TrackedPlayerId });
        });

        trackedPlayersGroup.MapPatch("/{id:guid}/enable", async (
            Guid id,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var actor = ResolveActor(principal);
            var result = await sender.Send(new EnableTrackedPlayerCommand(id, actor), cancellationToken);

            if (!result.IsSuccess)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.NoContent();
        });

        trackedPlayersGroup.MapPatch("/{id:guid}/disable", async (
            Guid id,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var actor = ResolveActor(principal);
            var result = await sender.Send(new DisableTrackedPlayerCommand(id, actor), cancellationToken);

            if (!result.IsSuccess)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.NoContent();
        });
    }
}

public sealed record AddTrackedPlayerRequest(long OsuUserId, TrackingTier TrackingTier = TrackingTier.Tier3);
public sealed record UpdateMarketSettingsRequest(decimal PpMultiplier, decimal TradeMultiplier, decimal DecayMultiplier, bool IsMaintenanceMode);
