using FluentValidation;

namespace OsuStocks.Application.Features.DailyLogin.GetDailyLoginStatus;

public sealed class GetDailyLoginStatusQueryValidator : AbstractValidator<GetDailyLoginStatusQuery>
{
    public GetDailyLoginStatusQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
