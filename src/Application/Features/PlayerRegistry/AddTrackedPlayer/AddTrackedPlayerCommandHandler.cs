using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.Repositories;
using System.Net;

namespace OsuStocks.Application.Features.PlayerRegistry.AddTrackedPlayer;

public sealed class AddTrackedPlayerCommandHandler(
    ITrackedPlayerRepository trackedPlayerRepository,
    IPlayerStockRepository playerStockRepository,
    IOsuOAuthService osuOAuthService,
    IOsuApiClient osuApiClient,
    IApplicationDbContext dbContext)
    : IRequestHandler<AddTrackedPlayerCommand, Result<AddTrackedPlayerResponse>>
{
    private const decimal InitialStockPrice = 100m;

    public async Task<Result<AddTrackedPlayerResponse>> Handle(
        AddTrackedPlayerCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await trackedPlayerRepository.GetByOsuUserIdAsync(request.OsuUserId, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<AddTrackedPlayerResponse>(
                "CONFLICT",
                $"osu user '{request.OsuUserId}' is already tracked.");
        }

        try
        {
            var token = await osuOAuthService.GetClientCredentialsTokenAsync(cancellationToken);
            var osuUser = await osuApiClient.GetUserAsync(
                request.OsuUserId,
                token.AccessToken,
                includeTopScore: false,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var actor = string.IsNullOrWhiteSpace(request.Actor) ? "admin" : request.Actor;

            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = osuUser.OsuUserId,
                Username = osuUser.Username,
                TrackingTier = request.TrackingTier,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = actor
            };

            await trackedPlayerRepository.AddAsync(trackedPlayer, cancellationToken);

            var stock = new PlayerStock
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = trackedPlayer.Id,
                CurrentPrice = InitialStockPrice,
                DemandScore = 0m,
                PerformanceScore = 0m,
                CreatedAt = now,
                CreatedBy = actor,
                LastUpdated = now
            };

            await playerStockRepository.AddAsync(stock, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AddTrackedPlayerResponse(
                trackedPlayer.Id,
                trackedPlayer.OsuUserId,
                trackedPlayer.Username,
                trackedPlayer.TrackingTier,
                trackedPlayer.IsActive));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Failure<AddTrackedPlayerResponse>(
                "NOT_FOUND",
                $"osu user '{request.OsuUserId}' was not found.");
        }
        catch (HttpRequestException)
        {
            return Result.Failure<AddTrackedPlayerResponse>(
                "OSU_API_UNAVAILABLE",
                "Unable to reach osu! API.");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AddTrackedPlayerResponse>("OAUTH_PROCESSING_FAILED", ex.Message);
        }
    }
}
