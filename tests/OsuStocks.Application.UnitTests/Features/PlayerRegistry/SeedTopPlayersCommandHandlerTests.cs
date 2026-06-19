using Microsoft.Extensions.Logging.Abstractions;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.PlayerRegistry.SeedTopPlayers;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using OsuStocks.Domain.Repositories;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.PlayerRegistry;

public sealed class SeedTopPlayersCommandHandlerTests
{
    private readonly FakeTrackedPlayerRepository _trackedPlayers = new();
    private readonly FakePlayerStockRepository _stocks = new();

    [Fact]
    public async Task Handle_SeedsRequestedCount_CreatingTrackedPlayersAndStocks()
    {
        // Three full pages (150 players) available; request the top 120.
        var handler = CreateHandler(BuildRanking(150));

        var result = await handler.Handle(new SeedTopPlayersCommand(120, "tester"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(120, result.Value!.Requested);
        Assert.Equal(120, result.Value.Fetched);
        Assert.Equal(120, result.Value.Added);
        Assert.Equal(0, result.Value.Skipped);
        Assert.Equal(120, _trackedPlayers.Items.Count);
        Assert.Equal(120, _stocks.Items.Count);
    }

    [Fact]
    public async Task Handle_AssignsTierAndPriceFromRank()
    {
        var handler = CreateHandler(BuildRanking(60));

        await handler.Handle(new SeedTopPlayersCommand(60, "tester"), default);

        var rank1 = _trackedPlayers.Items.Single(p => p.OsuUserId == OsuIdForRank(1));
        Assert.Equal(TrackingTier.Tier1, rank1.TrackingTier);

        var rank55 = _trackedPlayers.Items.Single(p => p.OsuUserId == OsuIdForRank(55));
        Assert.Equal(TrackingTier.Tier2, rank55.TrackingTier);

        // Top-ranked stock should be priced higher than a lower-ranked one.
        var topStock = _stocks.Items.Single(s => s.TrackedPlayerId == rank1.Id);
        var lowerStock = _stocks.Items.Single(s => s.TrackedPlayerId == rank55.Id);
        Assert.True(topStock.CurrentPrice > lowerStock.CurrentPrice);
    }

    [Fact]
    public async Task Handle_SkipsAlreadyTrackedPlayers_Idempotent()
    {
        // Rank-2 player is already tracked from a previous run.
        _trackedPlayers.Items.Add(new TrackedPlayer
        {
            Id = Guid.NewGuid(),
            OsuUserId = OsuIdForRank(2),
            Username = "existing",
            TrackingTier = TrackingTier.Tier1,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var handler = CreateHandler(BuildRanking(50));

        var result = await handler.Handle(new SeedTopPlayersCommand(50, "tester"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value!.Fetched);
        Assert.Equal(49, result.Value.Added);
        Assert.Equal(1, result.Value.Skipped);
        Assert.Equal(50, _trackedPlayers.Items.Count); // 1 pre-existing + 49 new
        Assert.Equal(49, _stocks.Items.Count);         // no stock for the skipped one
    }

    [Fact]
    public async Task Handle_StopsAtEndOfLeaderboard_WhenFewerThanRequested()
    {
        // Only 20 players exist but 100 requested.
        var handler = CreateHandler(BuildRanking(20));

        var result = await handler.Handle(new SeedTopPlayersCommand(100, "tester"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value!.Added);
        Assert.Equal(20, _trackedPlayers.Items.Count);
    }

    private SeedTopPlayersCommandHandler CreateHandler(IReadOnlyList<OsuUserProfile> ranking) =>
        new(
            new FakeOsuOAuthService(),
            new FakeRankingsApiClient(ranking),
            _trackedPlayers,
            _stocks,
            new FakeDbContext(),
            NullLogger<SeedTopPlayersCommandHandler>.Instance);

    private static long OsuIdForRank(int rank) => 100_000 + rank;

    private static IReadOnlyList<OsuUserProfile> BuildRanking(int count) =>
        Enumerable.Range(1, count)
            .Select(rank => new OsuUserProfile(
                OsuIdForRank(rank),
                $"player{rank}",
                null,
                10_000m - rank,
                rank,
                null,
                null,
                "US"))
            .ToList();

    private sealed class FakeOsuOAuthService : IOsuOAuthService
    {
        public string BuildAuthorizationUrl(string state) => throw new NotSupportedException();

        public Task<OsuOAuthToken> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OsuOAuthToken> GetClientCredentialsTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new OsuOAuthToken("token", null, DateTimeOffset.UtcNow.AddHours(1), "public"));
    }

    private sealed class FakeRankingsApiClient(IReadOnlyList<OsuUserProfile> ranking) : IOsuApiClient
    {
        private const int PageSize = 50;

        public Task<IReadOnlyList<OsuUserProfile>> GetPerformanceRankingsAsync(
            int page,
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            var pageItems = ranking.Skip((page - 1) * PageSize).Take(PageSize).ToList();
            return Task.FromResult<IReadOnlyList<OsuUserProfile>>(pageItems);
        }

        public Task<OsuUserProfile> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OsuUserProfile> GetUserAsync(long osuUserId, string accessToken, bool includeTopScore = true, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OsuTopScore?> GetTopScoreAsync(long osuUserId, string accessToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<OsuTopScore>> GetTopScoresAsync(long osuUserId, string accessToken, int limit = 10, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<OsuUserProfile>> SearchUsersAsync(string query, string accessToken, int limit = 10, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTrackedPlayerRepository : ITrackedPlayerRepository
    {
        public List<TrackedPlayer> Items { get; } = [];

        public Task<IReadOnlyList<TrackedPlayer>> GetAllAsync(bool? isActive = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrackedPlayer>>(Items.ToList());

        public Task AddAsync(TrackedPlayer trackedPlayer, CancellationToken cancellationToken = default)
        {
            Items.Add(trackedPlayer);
            return Task.CompletedTask;
        }

        public Task<TrackedPlayer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrackedPlayer?> GetByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TrackedPlayer>> GetByOsuUserIdsAsync(IReadOnlyCollection<long> osuUserIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<TrackedPlayer> Items, int TotalCount)> GetPagedAsync(bool? isActive, string? search, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TrackedPlayer>> GetActiveAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TrackedPlayer>> GetActiveByTierAsync(TrackingTier tier, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(TrackedPlayer trackedPlayer) => throw new NotSupportedException();
        public void Remove(TrackedPlayer trackedPlayer) => throw new NotSupportedException();
    }

    private sealed class FakePlayerStockRepository : IPlayerStockRepository
    {
        public List<PlayerStock> Items { get; } = [];

        public Task AddAsync(PlayerStock playerStock, CancellationToken cancellationToken = default)
        {
            Items.Add(playerStock);
            return Task.CompletedTask;
        }

        public Task<PlayerStock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PlayerStock?> GetByTrackedPlayerIdAsync(Guid trackedPlayerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerStock>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(PlayerStock playerStock) => throw new NotSupportedException();
    }

    private sealed class FakeDbContext : IApplicationDbContext
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
