using FluentValidation;
using OsuStocks.Application.Common.Interfaces;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.GetOsuLoginUrl;

public sealed class GetOsuLoginUrlQueryValidator : AbstractValidator<GetOsuLoginUrlQuery>
{
    public GetOsuLoginUrlQueryValidator(IOAuthReturnUrlPolicy returnUrlPolicy)
    {
        RuleFor(x => x.ReturnUrl)
            .MaximumLength(2048);

        RuleFor(x => x.ReturnUrl)
            .Must(returnUrlPolicy.IsAllowed)
            .When(x => !string.IsNullOrWhiteSpace(x.ReturnUrl))
            .WithMessage("Return URL origin is not allowed.");
    }
}
