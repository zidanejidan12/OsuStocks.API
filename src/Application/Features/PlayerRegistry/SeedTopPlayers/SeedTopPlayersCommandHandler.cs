using MediatR;
using Microsoft.Extensions.Logging;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Market.Services;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using OsuStocks.Domain.Repositories;
using System.Net.Http;

namespace OsuStocks.Application.Features.PlayerRegistry.SeedTopPlayers;

public sealed class SeedTopPlayersCommandHandler(
    IOsuOAuthService osuOAuthService,
    IOsuApiClient osuApiClient,
    ITrackedPlayerRepository trackedPlayerRepository,
    IPlayerStockRepository playerStockRepository,
    IApplicationDbContext dbContext,
    ILogger<SeedTopPlayersCommandHandler> logger)
    : IRequestHandler<SeedTopPlayersCommand, Result<SeedTopPlayersResponse>>
{
    private const int PageSize = 50;
    private const int MaxPlayers = 10_000; // osu! performance rankings cap (page 200 × 50).

    public async Task<Result<SeedTopPlayersResponse>> Handle(
        SeedTopPlayersCommand request,
        CancellationToken cancellationToken)
    {
        var actor = string.IsNullOrWhiteSpace(request.Actor) ? "seed" : request.Actor;
        var target = Math.Clamp(request.Count, 1, MaxPlayers);

        // Already-tracked ids drive idempotency: skip them so re-running only adds newcomers.
        var existing = await trackedPlayerRepository.GetAllAsync(cancellationToken: cancellationToken);
        var known = new HashSet<long>(existing.Select(static player => player.OsuUserId));

        string accessToken;
        try
        {
            var token = await osuOAuthService.GetClientCredentialsTokenAsync(cancellationToken);
            accessToken = token.AccessToken;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Top-player seed aborted: could not obtain an osu! API token.");
            return Result.Failure<SeedTopPlayersResponse>(
                "OSU_API_UNAVAILABLE", "Unable to obtain an osu! API token.");
        }

        var totalPages = (int)Math.Ceiling(target / (double)PageSize);
        var now = DateTimeOffset.UtcNow;
        var fetched = 0;
        var added = 0;
        var skipped = 0;

        for (var page = 1; page <= totalPages && fetched < target; page++)
        {
            IReadOnlyList<OsuUserProfile> ranking;
            try
            {
                ranking = await osuApiClient.GetPerformanceRankingsAsync(page, accessToken, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                // Keep whatever pages already succeeded; the next run resumes from where this stopped.
                logger.LogWarning(ex,
                    "Top-player seed stopped at page {Page}/{TotalPages} after an osu! API error. Added {Added} so far.",
                    page, totalPages, added);
                break;
            }

            if (ranking.Count == 0)
            {
                break; // Ran off the end of the leaderboard.
            }

            foreach (var entry in ranking)
            {
                if (fetched >= target)
                {
                    break;
                }

                fetched++;

                // known.Add returns false for an id already tracked OR already seen this run.
                if (entry.OsuUserId <= 0 || !known.Add(entry.OsuUserId))
                {
                    skipped++;
                    continue;
                }

                var trackedPlayer = new TrackedPlayer
                {
                    Id = Guid.NewGuid(),
                    OsuUserId = entry.OsuUserId,
                    Username = entry.Username,
                    AvatarUrl = entry.AvatarUrl,
                    CountryCode = entry.CountryCode,
                    ProfileCoverUrl = entry.ProfileCoverUrl,
                    TrackingTier = RankTierPolicy.TierForRank(entry.GlobalRank),
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = actor
                };
                await trackedPlayerRepository.AddAsync(trackedPlayer, cancellationToken);

                var stock = new PlayerStock
                {
                    Id = Guid.NewGuid(),
                    TrackedPlayerId = trackedPlayer.Id,
                    CurrentPrice = InitialStockPriceCalculator.Compute(entry.GlobalRank),
                    DemandScore = 0m,
                    PerformanceScore = 0m,
                    CreatedAt = now,
                    CreatedBy = actor,
                    LastUpdated = now
                };
                await playerStockRepository.AddAsync(stock, cancellationToken);

                added++;
            }

            // Persist per page so a mid-run failure keeps progress and the change tracker stays bounded.
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Top-player seed progress: page {Page}/{TotalPages}, fetched {Fetched}, added {Added}, skipped {Skipped}.",
                page, totalPages, fetched, added, skipped);
        }

        return Result.Success(new SeedTopPlayersResponse(target, fetched, added, skipped));
    }
}
