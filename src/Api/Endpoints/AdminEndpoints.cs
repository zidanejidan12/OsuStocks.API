using System.Security.Claims;
using Hangfire;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Admin.MarketSettings.GetMarketSettings;
using OsuStocks.Application.Features.Admin.MarketSettings.UpdateMarketSettings;
using OsuStocks.Application.Features.PlayerRegistry.AddTrackedPlayer;
using OsuStocks.Application.Features.PlayerRegistry.DeleteTrackedPlayer;
using OsuStocks.Application.Features.PlayerRegistry.DisableTrackedPlayer;
using OsuStocks.Application.Features.PlayerRegistry.EnableTrackedPlayer;
using OsuStocks.Application.Features.PlayerRegistry.ListTrackedPlayers;
using OsuStocks.Application.Features.PlayerRegistry.SearchOsuPlayers;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Infrastructure.BackgroundJobs;
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
                tradeFeeMultiplier = result.Value.TradeFeeMultiplier,
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
                    request.TradeFeeMultiplier,
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
            string? search,
            int? page,
            int? pageSize,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new ListTrackedPlayersQuery(isActive, search, page ?? 1, pageSize ?? 25),
                cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items,
                totalCount = result.Value.TotalCount,
                page = result.Value.Page,
                pageSize = result.Value.PageSize
            });
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

            return Results.Ok(new
            {
                trackedPlayerId = result.Value.TrackedPlayerId,
                osuUserId = result.Value.OsuUserId,
                username = result.Value.Username,
                trackingTier = result.Value.TrackingTier.ToString(),
                isActive = result.Value.IsActive,
                avatarUrl = result.Value.AvatarUrl,
                stockId = result.Value.StockId
            });
        });

        // Bulk-seed the current top-N osu! players as tracked stocks. Long-running, so it is
        // enqueued as a Hangfire job (executed on the worker) and returns 202 immediately.
        // Idempotent — already-tracked players are skipped, so it doubles as a "refresh latest".
        trackedPlayersGroup.MapPost("/seed", (
            int? count,
            ClaimsPrincipal principal,
            IBackgroundJobClient backgroundJobs) =>
        {
            var actor = ResolveActor(principal);
            var requested = Math.Clamp(count ?? 5000, 1, 10_000);

            var jobId = backgroundJobs.Enqueue<SeedTrackedPlayersJob>(job => job.RunAsync(requested, actor));

            return Results.Accepted(
                "/api/v1/admin/tracked-players",
                new { jobId, count = requested, status = "queued" });
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

        trackedPlayersGroup.MapDelete("/{id:guid}", async (
            Guid id,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DeleteTrackedPlayerCommand(id), cancellationToken);

            if (!result.IsSuccess)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.NoContent();
        });
    }
}

public sealed record AddTrackedPlayerRequest(long OsuUserId, TrackingTier TrackingTier = TrackingTier.Tier3);
public sealed record UpdateMarketSettingsRequest(decimal PpMultiplier, decimal TradeMultiplier, decimal DecayMultiplier, decimal TradeFeeMultiplier, bool IsMaintenanceMode);
