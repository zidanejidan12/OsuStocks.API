using FluentValidation;

namespace OsuStocks.Application.Features.DailyLogin.ClaimDailyLoginReward;

public sealed class ClaimDailyLoginRewardCommandValidator : AbstractValidator<ClaimDailyLoginRewardCommand>
{
    public ClaimDailyLoginRewardCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
