namespace OsuStocks.Application.Features.DailyLogin.ClaimDailyLoginReward;

public sealed record ClaimDailyLoginRewardResponse(
    bool Granted,
    bool AlreadyClaimed,
    decimal Amount,
    int StreakDay,
    decimal NewBalance);
