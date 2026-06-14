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
    // Rank-based opening price: a power-law curve over global rank so stronger players list higher.
    // price = TopPrice * rank^(-Decay), floored at MinPrice. Tuned so rank 1 ≈ 1000 and rank 500 ≈ 100.
    // Unranked players (no global rank) fall back to the neutral baseline.
    private const decimal TopPrice = 1000m;
    private const double RankDecay = 0.37;
    private const decimal MinPrice = 1m;
    private const decimal UnrankedPrice = 100m;

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
                AvatarUrl = osuUser.AvatarUrl,
                CountryCode = osuUser.CountryCode,
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
                CurrentPrice = ComputeInitialStockPrice(osuUser.GlobalRank),
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

    private static decimal ComputeInitialStockPrice(int? globalRank)
    {
        if (globalRank is null or <= 0)
        {
            return UnrankedPrice;
        }

        var price = (decimal)((double)TopPrice * Math.Pow(globalRank.Value, -RankDecay));
        return Math.Round(Math.Max(price, MinPrice), 2);
    }
}
