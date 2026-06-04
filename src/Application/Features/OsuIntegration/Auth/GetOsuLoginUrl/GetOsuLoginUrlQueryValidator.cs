using FluentValidation;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.GetOsuLoginUrl;

public sealed class GetOsuLoginUrlQueryValidator : AbstractValidator<GetOsuLoginUrlQuery>
{
    public GetOsuLoginUrlQueryValidator()
    {
        RuleFor(x => x.ReturnUrl)
            .MaximumLength(2048);
    }
}
