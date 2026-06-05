using OsuStocks.Application.Features.OsuIntegration.Synchronization.Services;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Events;
using OsuStocks.Domain.OsuIntegration.Models;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.OsuIntegration.Synchronization.Services;

public sealed class SnapshotComparisonServiceTests
{
    private readonly SnapshotComparisonService _service = new();

    [Fact]
    public void Compare_WhenPpIncreases_EmitsPpIncreasedEvent()
    {
        var trackedPlayerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var previousSnapshot = CreateSnapshot(
            trackedPlayerId,
            currentPp: 10_000m,
            topScoreId: 111,
            capturedAt: now.AddDays(-1));

        var currentProfile = new OsuUserProfile(
            OsuUserId: 1001,
            Username: "mrekk",
            AvatarUrl: "https://avatar.example/mrekk",
            CurrentPp: 10_250m,
            GlobalRank: 1,
            TopScoreId: 111,
            TopScorePp: 1_000m);

        var result = _service.Compare(previousSnapshot, currentProfile, trackedPlayerId, now);

        Assert.False(result.IsInactive);

        var @event = Assert.Single(result.Events);
        var ppIncreased = Assert.IsType<PpIncreased>(@event);

        Assert.Equal(trackedPlayerId, ppIncreased.TrackedPlayerId);
        Assert.Equal(10_000m, ppIncreased.PreviousPp);
        Assert.Equal(10_250m, ppIncreased.CurrentPp);
        Assert.Equal(now, ppIncreased.OccurredAt);
    }

    [Fact]
    public void Compare_WhenTopPlayChanges_EmitsTopPlayDetectedEvent()
    {
        var trackedPlayerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var previousSnapshot = CreateSnapshot(
            trackedPlayerId,
            currentPp: 11_000m,
            topScoreId: 111,
            capturedAt: now.AddDays(-1));

        var currentProfile = new OsuUserProfile(
            OsuUserId: 1001,
            Username: "mrekk",
            AvatarUrl: "https://avatar.example/mrekk",
            CurrentPp: 11_000m,
            GlobalRank: 1,
            TopScoreId: 222,
            TopScorePp: 1_111m);

        var result = _service.Compare(previousSnapshot, currentProfile, trackedPlayerId, now);

        Assert.False(result.IsInactive);

        var @event = Assert.Single(result.Events);
        var topPlayDetected = Assert.IsType<TopPlayDetected>(@event);

        Assert.Equal(trackedPlayerId, topPlayDetected.TrackedPlayerId);
        Assert.Equal(111, topPlayDetected.PreviousTopScoreId);
        Assert.Equal(222, topPlayDetected.NewTopScoreId);
        Assert.Equal(1_111m, topPlayDetected.NewTopScorePp);
        Assert.Equal(now, topPlayDetected.OccurredAt);
    }

    [Fact]
    public void Compare_WhenNoRelevantChange_EmitsNoEvents()
    {
        var trackedPlayerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var previousSnapshot = CreateSnapshot(
            trackedPlayerId,
            currentPp: 12_000m,
            topScoreId: 333,
            capturedAt: now.AddDays(-1));

        var currentProfile = new OsuUserProfile(
            OsuUserId: 1001,
            Username: "mrekk",
            AvatarUrl: "https://avatar.example/mrekk",
            CurrentPp: 12_000m,
            GlobalRank: 1,
            TopScoreId: 333,
            TopScorePp: 1_200m);

        var result = _service.Compare(previousSnapshot, currentProfile, trackedPlayerId, now);

        Assert.False(result.IsInactive);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Compare_WhenSnapshotIsOldAndNoPpGain_EmitsPlayerInactiveEvent()
    {
        var trackedPlayerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var previousSnapshot = CreateSnapshot(
            trackedPlayerId,
            currentPp: 13_000m,
            topScoreId: 444,
            capturedAt: now.AddDays(-14));

        var currentProfile = new OsuUserProfile(
            OsuUserId: 1001,
            Username: "mrekk",
            AvatarUrl: "https://avatar.example/mrekk",
            CurrentPp: 13_000m,
            GlobalRank: 1,
            TopScoreId: 444,
            TopScorePp: 1_300m);

        var result = _service.Compare(previousSnapshot, currentProfile, trackedPlayerId, now);

        Assert.True(result.IsInactive);

        var @event = Assert.Single(result.Events);
        var inactive = Assert.IsType<PlayerInactive>(@event);

        Assert.Equal(trackedPlayerId, inactive.TrackedPlayerId);
        Assert.Equal(now, inactive.OccurredAt);
    }

    private static PlayerSnapshot CreateSnapshot(
        Guid trackedPlayerId,
        decimal currentPp,
        long? topScoreId,
        DateTimeOffset capturedAt)
    {
        return new PlayerSnapshot
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = trackedPlayerId,
            CurrentPp = currentPp,
            TopScoreId = topScoreId,
            CapturedAt = capturedAt
        };
    }
}
