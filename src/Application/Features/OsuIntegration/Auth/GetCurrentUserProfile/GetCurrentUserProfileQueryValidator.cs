using FluentValidation;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.GetCurrentUserProfile;

public sealed class GetCurrentUserProfileQueryValidator : AbstractValidator<GetCurrentUserProfileQuery>
{
    public GetCurrentUserProfileQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
