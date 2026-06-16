using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.DailyLogin.GetDailyLoginStatus;

public sealed class GetDailyLoginStatusQueryHandler(
    IDailyLoginRewardRepository dailyLoginRewardRepository,
    IDailyRewardSettings settings)
    : IRequestHandler<GetDailyLoginStatusQuery, Result<DailyLoginStatusResponse>>
{
    public async Task<Result<DailyLoginStatusResponse>> Handle(
        GetDailyLoginStatusQuery request,
        CancellationToken cancellationToken)
    {
        var schedule = settings.DailyAmounts;
        if (schedule is null || schedule.Count == 0)
        {
            return Result.Failure<DailyLoginStatusResponse>(
                "CONFIGURATION_ERROR", "Daily reward schedule is not configured.");
        }

        var today = DailyLoginClock.ServerToday();

        // Derive state from the ledger (authoritative), not the denormalized User cache.
        var latest = await dailyLoginRewardRepository.GetLatestByUserAsync(request.UserId, cancellationToken);
        var projection = DailyRewardStreakCalculator.Compute(
            latest?.RewardDate, latest?.StreakDay ?? 0, today, schedule.Count);

        int streak;
        bool claimedToday;
        decimal todayAmount;

        if (projection.AlreadyClaimed)
        {
            claimedToday = true;
            streak = latest!.StreakDay;
            todayAmount = latest.Amount;
        }
        else
        {
            claimedToday = false;
            // Report the cycle day that today's claim corresponds to, so `streak` and `todayAmount` always
            // describe the SAME day and match the StreakDay the claim will persist. A first-ever claim or a
            // lapsed streak is day 1; a consecutive claim advances, wrapping back to day 1 after the final day.
            streak = projection.StreakDay;
            todayAmount = schedule[projection.StreakDay - 1];
        }

        var nextResetUtc = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        return Result.Success(new DailyLoginStatusResponse(
            streak,
            claimedToday,
            todayAmount,
            schedule,
            DateTimeOffset.UtcNow,
            nextResetUtc));
    }
}
