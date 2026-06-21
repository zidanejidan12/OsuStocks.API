using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Profile.UpdateProfileShowcase;
using OsuStocks.Domain.Achievements.Models;
using OsuStocks.Domain.Achievements.Services;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Profile;

public sealed class UpdateProfileShowcaseCommandHandlerTests
{
    private readonly AchievementCatalog _catalog = new();
    private readonly User _user = new() { Id = Guid.NewGuid(), Username = "tester" };

    [Fact]
    public async Task Handle_EquipAndShowcaseUnlockedAchievements_PersistsAndResolvesTitle()
    {
        var handler = CreateHandler("first-trade", "trades-10");

        var result = await handler.Handle(
            new UpdateProfileShowcaseCommand(_user.Id, "first-trade", ["trades-10", "first-trade"]),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal("first-trade", result.Value!.EquippedTitleCode);
        Assert.Equal("First Steps", result.Value.EquippedTitle); // resolved from the catalog
        Assert.Equal(new[] { "trades-10", "first-trade" }, result.Value.ShowcasedAchievementCodes);
        Assert.Equal("first-trade", _user.EquippedTitleCode);
        Assert.Equal(new List<string> { "trades-10", "first-trade" }, _user.ShowcasedAchievementCodes);
    }

    [Fact]
    public async Task Handle_ShowcaseNotUnlockedAchievement_FailsConflict_AndDoesNotMutate()
    {
        var handler = CreateHandler("first-trade"); // only first-trade unlocked

        var result = await handler.Handle(
            new UpdateProfileShowcaseCommand(_user.Id, null, ["trades-100"]),
            default);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONFLICT", result.Error!.Code);
        Assert.Null(_user.EquippedTitleCode);
        Assert.Empty(_user.ShowcasedAchievementCodes);
    }

    [Fact]
    public async Task Handle_EquipNotUnlockedTitle_FailsConflict()
    {
        var handler = CreateHandler("first-trade");

        var result = await handler.Handle(
            new UpdateProfileShowcaseCommand(_user.Id, "level-25", []),
            default);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONFLICT", result.Error!.Code);
    }

    [Fact]
    public async Task Handle_UnknownCode_FailsNotFound()
    {
        var handler = CreateHandler("first-trade");

        var result = await handler.Handle(
            new UpdateProfileShowcaseCommand(_user.Id, null, ["does-not-exist"]),
            default);

        Assert.False(result.IsSuccess);
        Assert.Equal("NOT_FOUND", result.Error!.Code);
    }

    private UpdateProfileShowcaseCommandHandler CreateHandler(params string[] unlockedCodes) =>
        new(
            new FakeUserRepository(_user),
            new FakeUserAchievementRepository(unlockedCodes),
            _catalog,
            new NoOpDbContext());

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(user);
        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<User?> GetByOsuUserIdAsync(long osuUserId, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<bool> ExistsByOsuUserIdAsync(long osuUserId, CancellationToken ct = default) => Task.FromResult(false);
        public Task AddAsync(User u, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(User u) { }
    }

    private sealed class FakeUserAchievementRepository(string[] unlocked) : IUserAchievementRepository
    {
        public Task<IReadOnlyList<AchievementUnlockReadModel>> GetUnlockedAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AchievementUnlockReadModel>>(
                unlocked.Select(c => new AchievementUnlockReadModel(c, DateTimeOffset.UnixEpoch)).ToList());

        public Task<bool> TryUnlockAndRewardAsync(Guid userId, string achievementCode, long rewardCredits, DateTimeOffset occurredAt, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private sealed class NoOpDbContext : IApplicationDbContext
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
